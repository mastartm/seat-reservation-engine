import { act, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { ReservationView } from '../api/types'
import { resetServerTimeForTests } from '../lib/clock'
import { HoldCard } from './HoldCard'

const T0 = Date.parse('2030-01-01T12:00:00Z')

function hold(): ReservationView {
  return {
    id: 'res-1', seatId: 'seat-1', eventId: 'ev-1', seatLabel: 'A3', status: 'Held',
    createdAt: new Date(T0).toISOString(), expiresAt: new Date(T0 + 600_000).toISOString(), confirmedAt: null,
  }
}

beforeEach(() => {
  vi.useFakeTimers()
  vi.setSystemTime(T0)
  resetServerTimeForTests()
})
afterEach(() => vi.useRealTimers())

describe('HoldCard geri sayımı', () => {
  it('10 dakikayla başlar ve saniye saniye azalır', () => {
    render(<HoldCard reservation={hold()} onExpire={() => {}} onConfirmed={() => {}} />)
    expect(screen.getByRole('timer')).toHaveTextContent('10:00')

    act(() => { vi.advanceTimersByTime(65_000) })
    expect(screen.getByRole('timer')).toHaveTextContent('08:55')
  })

  it('son dakikada uyarı rengine döner', () => {
    render(<HoldCard reservation={hold()} onExpire={() => {}} onConfirmed={() => {}} />)
    expect(screen.getByRole('timer')).not.toHaveClass('text-red-600')

    act(() => { vi.advanceTimersByTime(545_000) })
    expect(screen.getByRole('timer')).toHaveTextContent('00:55')
    expect(screen.getByRole('timer')).toHaveClass('text-red-600')
  })

  it('süre dolunca onExpire bir kez çağrılır, onay düğmesi kalkar, kart "süre doldu" der', () => {
    const onExpire = vi.fn()
    render(<HoldCard reservation={hold()} onExpire={onExpire} onConfirmed={() => {}} />)

    act(() => { vi.advanceTimersByTime(600_000) })
    act(() => { vi.advanceTimersByTime(5_000) })

    expect(onExpire).toHaveBeenCalledTimes(1)
    expect(screen.queryByRole('button', { name: /satın almayı onayla/ })).not.toBeInTheDocument()
    expect(screen.getByText(/süre doldu/)).toBeInTheDocument()
  })

  it('kullanıcının saati yanlış olsa da sunucu saatine göre sayar', async () => {
    const { syncServerTime } = await import('../lib/clock')
    // Kullanıcının saati sunucudan 1 saat ileri: düzeltme olmasa kart baştan "süre doldu" derdi.
    vi.setSystemTime(T0 + 3_600_000)
    syncServerTime(new Date(T0).toUTCString())

    render(<HoldCard reservation={hold()} onExpire={() => {}} onConfirmed={() => {}} />)
    // Başlık saniyeyi aşağı yuvarladığı için ±1 sn tolerans; asıl nokta "süre doldu" dememesi.
    expect(screen.getByRole('timer')).toHaveTextContent(/^(10:01|10:00)$/)
  })
})
