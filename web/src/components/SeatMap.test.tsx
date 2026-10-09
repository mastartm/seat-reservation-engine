import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { ReservationView, SeatView } from '../api/types'
import { seatDisplayState } from '../lib/seatState'
import { SeatMap } from './SeatMap'

const seats: SeatView[] = [
  { id: 's-a1', label: 'A1', status: 'Available' },
  { id: 's-a2', label: 'A2', status: 'Held' },
  { id: 's-a10', label: 'A10', status: 'Sold' },
  { id: 's-b1', label: 'B1', status: 'Held' },
  { id: 's-b2', label: 'B2', status: 'Sold' },
]

function reservation(seatId: string, status: ReservationView['status']): ReservationView {
  return {
    id: `r-${seatId}`, seatId, eventId: 'e1', seatLabel: 'X', status,
    createdAt: '2026-01-01T10:00:00Z', expiresAt: '2026-01-01T10:10:00Z', confirmedAt: null,
  }
}

describe('seatDisplayState', () => {
  it('kendi tuttuğum ve satın aldığım koltukları başkalarınınkinden ayırır', () => {
    const mine = new Map([
      ['s-b1', reservation('s-b1', 'Held')],
      ['s-b2', reservation('s-b2', 'Confirmed')],
    ])
    expect(seatDisplayState(seats[0], mine)).toBe('available')
    expect(seatDisplayState(seats[1], mine)).toBe('held')
    expect(seatDisplayState(seats[3], mine)).toBe('mine-held')
    expect(seatDisplayState(seats[4], mine)).toBe('mine-sold')
    expect(seatDisplayState(seats[2], mine)).toBe('sold')
  })

  it('rezervasyonum Held görünse de sunucu koltuğu artık boş diyorsa koltuk boştur', () => {
    const mine = new Map([['s-a1', reservation('s-a1', 'Held')]])
    expect(seatDisplayState(seats[0], mine)).toBe('available')
  })
})

describe('SeatMap', () => {
  it('koltukları satırlara böler ve numaraya göre sıralar (A2, A10\'dan önce)', () => {
    render(<SeatMap seats={seats} mine={new Map()} />)
    const labels = screen.getAllByRole('button').map((b) => b.getAttribute('aria-label')?.split(',')[0])
    expect(labels).toEqual(['A1', 'A2', 'A10', 'B1', 'B2'])
  })

  it('her koltuğun durumunu erişilebilir adında söyler', () => {
    render(<SeatMap seats={seats} mine={new Map()} />)
    expect(screen.getByRole('button', { name: 'A1, boş' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'A2, başkası tutuyor' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'A10, satıldı' })).toBeInTheDocument()
  })

  it('yalnızca boş koltuk seçilebilir', async () => {
    const onSelect = vi.fn()
    render(<SeatMap seats={seats} mine={new Map()} onSelect={onSelect} />)
    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: 'A2, başkası tutuyor' }))
    await user.click(screen.getByRole('button', { name: 'A10, satıldı' }))
    expect(onSelect).not.toHaveBeenCalled()

    await user.click(screen.getByRole('button', { name: 'A1, boş' }))
    expect(onSelect).toHaveBeenCalledWith(seats[0])
  })

  it('onSelect verilmediyse (salt okunur) hiçbir koltuk tıklanamaz', () => {
    render(<SeatMap seats={seats} mine={new Map()} />)
    expect(screen.getByRole('button', { name: 'A1, boş' })).toBeDisabled()
  })

  it('bir istek sürerken diğer koltuklar kilitlenir', () => {
    render(<SeatMap seats={seats} mine={new Map()} onSelect={() => {}} busySeatId="s-a1" />)
    expect(screen.getByRole('button', { name: 'A1, boş' })).toBeDisabled()
  })
})
