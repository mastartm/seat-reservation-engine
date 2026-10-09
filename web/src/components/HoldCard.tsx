import { useEffect, useRef, useState } from 'react'
import { ApiError, api } from '../api/client'
import type { ReservationView } from '../api/types'
import { formatRemaining } from '../lib/format'
import { useServerNow } from '../lib/useServerNow'

interface Props {
  reservation: ReservationView
  /** Süre sıfırlandığında bir kez çağrılır (üst bileşen verileri yeniler ve "süre doldu" bilgisini saklar). */
  onExpire: (reservation: ReservationView) => void
  /** Onay başarıyla tamamlanınca. */
  onConfirmed: (reservation: ReservationView) => void
  /** "Süre doldu" kartını kapatır. */
  onDismiss?: (reservation: ReservationView) => void
}

const WARNING_MS = 60_000

/**
 * Tutulan koltuğun 10 dakikalık geri sayımı ve onay düğmesi.
 * Kalan süre sunucunun verdiği `expiresAt`'ten, sunucu saatine göre hesaplanır (bkz. lib/clock.ts);
 * asıl yetki yine sunucudadır: süre dolduktan sonra gelen onay isteği orada 409 ile reddedilir.
 */
export function HoldCard({ reservation, onExpire, onConfirmed, onDismiss }: Props) {
  const now = useServerNow(1000)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const total = Date.parse(reservation.expiresAt) - Date.parse(reservation.createdAt)
  const remaining = Date.parse(reservation.expiresAt) - now
  const expired = remaining <= 0

  const notified = useRef(false)
  useEffect(() => {
    if (expired && !notified.current) {
      notified.current = true
      onExpire(reservation)
    }
  }, [expired, onExpire, reservation])

  async function confirm() {
    setBusy(true)
    setError(null)
    try {
      onConfirmed(await api.confirm(reservation.id))
    } catch (e) {
      // 409: süre dolmuş ya da koltuk artık bizde değil; kullanıcı nedenini sunucunun mesajından okur.
      setError(e instanceof ApiError ? e.message : 'Onaylama başarısız oldu.')
      setBusy(false)
    }
  }

  if (expired) {
    return (
      <li className="flex items-center justify-between gap-3 rounded-xl border border-slate-200 bg-slate-100 p-4 text-sm text-slate-600">
        <span>
          <strong>{reservation.seatLabel}</strong> için süre doldu, koltuk serbest bırakıldı.
        </span>
        {onDismiss && (
          <button type="button" onClick={() => onDismiss(reservation)} className="text-indigo-600 hover:underline">
            Kapat
          </button>
        )}
      </li>
    )
  }

  const urgent = remaining <= WARNING_MS
  const percent = Math.min(100, Math.max(0, (remaining / total) * 100))

  return (
    <li className="space-y-3 rounded-xl border border-indigo-200 bg-indigo-50 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <p className="text-sm text-slate-600">Koltuk senin için tutuluyor</p>
          <p className="text-xl font-bold text-indigo-900">{reservation.seatLabel}</p>
        </div>
        <div className="text-right">
          <p className="text-xs text-slate-500">Kalan süre</p>
          <p
            role="timer"
            aria-label={`${reservation.seatLabel} için kalan süre`}
            className={`font-mono text-2xl font-bold tabular-nums ${urgent ? 'text-red-600' : 'text-indigo-900'}`}
          >
            {formatRemaining(remaining)}
          </p>
        </div>
      </div>

      <div className="h-1.5 overflow-hidden rounded-full bg-indigo-100" aria-hidden="true">
        <div
          className={`h-full transition-[width] duration-1000 ease-linear ${urgent ? 'bg-red-500' : 'bg-indigo-500'}`}
          style={{ width: `${percent}%` }}
        />
      </div>

      {error && (
        <p role="alert" className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">
          {error}
        </p>
      )}

      <button
        type="button"
        onClick={confirm}
        disabled={busy}
        className="w-full rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-60"
      >
        {busy ? 'Onaylanıyor…' : `${reservation.seatLabel} için satın almayı onayla`}
      </button>
    </li>
  )
}
