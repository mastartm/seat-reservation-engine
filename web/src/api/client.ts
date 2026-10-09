import { syncServerTime } from '../lib/clock'
import type { AuthResult, EventSummary, ReservationView, SeatView } from './types'

const baseUrl = (import.meta.env.VITE_API_URL ?? '').replace(/\/$/, '')

/** Sunucunun ProblemDetails yanıtını (Detail alanı kullanıcıya gösterilebilir Türkçe mesajdır) taşır. */
export class ApiError extends Error {
  readonly status: number
  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

let token: string | null = null
let onUnauthorized: (() => void) | null = null

export function setAuthToken(value: string | null): void {
  token = value
}

/** Token sunucuca reddedilince (süresi dolmuş/geçersiz) oturumu kapatmak için AuthProvider kaydeder. */
export function setUnauthorizedHandler(handler: (() => void) | null): void {
  onUnauthorized = handler
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  const sentToken = token
  if (sentToken) headers.Authorization = `Bearer ${sentToken}`

  let response: Response
  try {
    response = await fetch(`${baseUrl}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, 'Sunucuya ulaşılamadı. Bağlantını kontrol edip tekrar dene.')
  }

  syncServerTime(response.headers.get('Date'))

  if (!response.ok) {
    // Giriş denemesindeki 401 "parola yanlış" demektir; yalnızca taşıdığımız token reddedildiyse oturum kapanır.
    if (response.status === 401 && sentToken) onUnauthorized?.()
    throw new ApiError(response.status, await readProblemMessage(response))
  }
  return (await response.json()) as T
}

async function readProblemMessage(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as { detail?: string; title?: string; errors?: Record<string, string[]> }
    if (problem.detail) return problem.detail
    // [ApiController] model doğrulaması 400'ünde mesajlar `errors` altında gelir.
    const first = problem.errors && Object.values(problem.errors).flat()[0]
    return first ?? problem.title ?? `İstek başarısız (${response.status}).`
  } catch {
    return `İstek başarısız (${response.status}).`
  }
}

export const api = {
  register: (email: string, password: string) =>
    request<AuthResult>('POST', '/api/auth/register', { email, password }),
  login: (email: string, password: string) =>
    request<AuthResult>('POST', '/api/auth/login', { email, password }),
  listEvents: () => request<EventSummary[]>('GET', '/api/events'),
  listSeats: (eventId: string) => request<SeatView[]>('GET', `/api/events/${eventId}/seats`),
  holdSeat: (seatId: string) => request<ReservationView>('POST', `/api/seats/${seatId}/hold`),
  confirm: (reservationId: string) =>
    request<ReservationView>('POST', `/api/reservations/${reservationId}/confirm`),
  myReservations: () => request<ReservationView[]>('GET', '/api/reservations/mine'),
}
