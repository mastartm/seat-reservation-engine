import { act, render, screen, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { ReservationView } from '../api/types'
import { resetServerTimeForTests } from '../lib/clock'
import { EVENT } from '../test/fakeApi'
import { MyReservationsPage } from './MyReservationsPage'

const T0 = Date.parse('2030-01-01T12:00:00Z')

function reservation(overrides: Partial<ReservationView>): ReservationView {
  return {
    id: 'r', seatId: 's', eventId: EVENT.id, seatLabel: 'A1', status: 'Held',
    createdAt: new Date(T0).toISOString(), expiresAt: new Date(T0 + 600_000).toISOString(), confirmedAt: null,
    ...overrides,
  }
}

function renderPage(reservations: ReservationView[] | undefined, error: Error | null = null) {
  return render(
    <MyReservationsPage reservations={reservations} error={error} events={[EVENT]} onChanged={() => {}} onBrowse={() => {}} />,
  )
}

beforeEach(() => {
  vi.useFakeTimers()
  vi.setSystemTime(T0)
  resetServerTimeForTests()
})
afterEach(() => vi.useRealTimers())

describe('MyReservationsPage', () => {
  it('yüklenirken ve hata durumunda ilgili mesajı gösterir', () => {
    const { unmount } = renderPage(undefined)
    expect(screen.getByText('Rezervasyonlar yükleniyor…')).toBeInTheDocument()
    unmount()
    renderPage(undefined, new Error('Sunucuya ulaşılamadı.'))
    expect(screen.getByRole('alert')).toHaveTextContent('Sunucuya ulaşılamadı.')
  })

  it('rezervasyon yoksa boş durum gösterir', () => {
    renderPage([])
    expect(screen.getByText('Henüz rezervasyonun yok.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Koltuk seç' })).toBeInTheDocument()
  })

  it('aktif tutma geri sayımla, onaylı ve süresi dolmuş kayıtlar rozetle listelenir; aktif olan en üsttedir', () => {
    renderPage([
      reservation({ id: 'old', seatLabel: 'C1', status: 'Expired', createdAt: new Date(T0 - 3_600_000).toISOString() }),
      reservation({ id: 'bought', seatLabel: 'B2', status: 'Confirmed', confirmedAt: new Date(T0 - 60_000).toISOString(), createdAt: new Date(T0 - 120_000).toISOString() }),
      reservation({ id: 'live', seatLabel: 'A1' }),
    ])

    const items = within(screen.getByRole('list')).getAllByRole('listitem')
    expect(items).toHaveLength(3)
    expect(within(items[0]).getByRole('timer', { name: 'A1 için kalan süre' })).toHaveTextContent('10:00')
    expect(within(items[1]).getByText('Satın alındı')).toBeInTheDocument()
    expect(within(items[1]).getByText(/Gece Konseri/)).toBeInTheDocument()
    expect(within(items[2]).getByText('Süresi doldu')).toBeInTheDocument()
  })

  it('arka plan servisi henüz Expired yapmamış ama süresi geçmiş tutma "Süresi doldu" görünür', () => {
    renderPage([reservation({ id: 'stale', seatLabel: 'A5', createdAt: new Date(T0 - 700_000).toISOString(), expiresAt: new Date(T0 - 100_000).toISOString() })])
    expect(screen.getByText('Süresi doldu')).toBeInTheDocument()
    expect(screen.queryByRole('timer')).not.toBeInTheDocument()
  })

  it('geri sayım biterken aktif kart rozete dönüşür', () => {
    renderPage([reservation({ id: 'live', seatLabel: 'A1' })])
    expect(screen.getByRole('timer')).toBeInTheDocument()

    act(() => { vi.advanceTimersByTime(601_000) })
    expect(screen.queryByRole('timer')).not.toBeInTheDocument()
    expect(screen.getByText('Süresi doldu')).toBeInTheDocument()
  })
})
