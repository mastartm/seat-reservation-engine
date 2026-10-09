import { vi } from 'vitest'
import type { AuthResult, EventSummary, ReservationView, SeatStatus, SeatView } from '../api/types'

export const EVENT: EventSummary = { id: 'ev-1', name: 'Gece Konseri', startsAt: '2030-01-01T18:00:00Z' }

export const SESSION: AuthResult = {
  token: 'test-token',
  expiresAt: '2099-01-01T00:00:00Z',
  userId: 'user-1',
  email: 'ali@example.com',
  role: 'User',
}

/**
 * Sunucuyu taklit eden, durumlu bir fetch sahtesi: hold/confirm gerçekten koltuk ve rezervasyon durumunu değiştirir.
 * Böylece testler tek tek istekleri değil, kullanıcının gördüğü akışı doğrular.
 */
export function installFakeApi(options: { holdMs?: number; failHoldWith?: { status: number; detail: string } } = {}) {
  const holdMs = options.holdMs ?? 600_000
  const seats: SeatView[] = ['A1', 'A2', 'B1'].map((label, i) => ({ id: `seat-${i}`, label, status: 'Available' as SeatStatus }))
  const reservations: ReservationView[] = []
  let sequence = 0

  const json = (status: number, body: unknown) =>
    new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json', Date: new Date().toUTCString() } })

  const handler = async (url: string, init: RequestInit = {}) => {
    const method = init.method ?? 'GET'
    if (url === '/api/auth/demo' && method === 'POST') return json(201, { ...SESSION, email: 'misafir-test@demo.local' })
    if (url === '/api/events') return json(200, [EVENT])
    if (url === `/api/events/${EVENT.id}/seats`) return json(200, seats)
    if (url === '/api/reservations/mine') return json(200, [...reservations].reverse())

    const hold = /^\/api\/seats\/(.+)\/hold$/.exec(url)
    if (hold && method === 'POST') {
      if (options.failHoldWith) return json(options.failHoldWith.status, { detail: options.failHoldWith.detail })
      const seat = seats.find((s) => s.id === hold[1])!
      seat.status = 'Held'
      const createdAt = new Date()
      const reservation: ReservationView = {
        id: `res-${++sequence}`, seatId: seat.id, eventId: EVENT.id, seatLabel: seat.label, status: 'Held',
        createdAt: createdAt.toISOString(), expiresAt: new Date(createdAt.getTime() + holdMs).toISOString(), confirmedAt: null,
      }
      reservations.push(reservation)
      return json(201, reservation)
    }

    const confirm = /^\/api\/reservations\/(.+)\/confirm$/.exec(url)
    if (confirm && method === 'POST') {
      const reservation = reservations.find((r) => r.id === confirm[1])!
      reservation.status = 'Confirmed'
      reservation.confirmedAt = new Date().toISOString()
      seats.find((s) => s.id === reservation.seatId)!.status = 'Sold'
      return json(200, reservation)
    }
    return json(404, { detail: `fakeApi: ${method} ${url} tanımsız` })
  }

  const fetchMock = vi.fn(handler)
  vi.stubGlobal('fetch', fetchMock)
  return { fetchMock, seats, reservations }
}

export function signIn() {
  localStorage.setItem('seat-reservation.session', JSON.stringify(SESSION))
}
