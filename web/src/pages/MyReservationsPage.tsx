import type { EventSummary, ReservationView } from '../api/types'
import { HoldCard } from '../components/HoldCard'
import { formatDateTime } from '../lib/format'
import { useServerNow } from '../lib/useServerNow'

interface Props {
  /** `undefined`: henüz yüklenmedi. */
  reservations: readonly ReservationView[] | undefined
  error: Error | null
  events: readonly EventSummary[]
  onChanged: () => void
  onBrowse: () => void
}

type Effective = 'active' | 'confirmed' | 'expired'

/**
 * Sunucu süresi dolan tutmayı arka plan servisiyle (30 sn aralıkla) "Expired"a çevirir; o ana kadar kayıt "Held"
 * görünür. Kullanıcıya hep gerçek durumu göstermek için süresi geçmiş Held kaydı burada "expired" sayılır.
 */
function effectiveState(r: ReservationView, now: number): Effective {
  if (r.status === 'Confirmed') return 'confirmed'
  if (r.status === 'Held' && Date.parse(r.expiresAt) > now) return 'active'
  return 'expired'
}

const BADGE: Record<Effective, { text: string; className: string }> = {
  active: { text: 'Tutuluyor', className: 'bg-indigo-100 text-indigo-800' },
  confirmed: { text: 'Satın alındı', className: 'bg-emerald-100 text-emerald-800' },
  expired: { text: 'Süresi doldu', className: 'bg-slate-200 text-slate-600' },
}

export function MyReservationsPage({ reservations, error, events, onChanged, onBrowse }: Props) {
  const now = useServerNow(1000)
  const eventName = (id: string) => events.find((e) => e.id === id)?.name ?? 'Etkinlik'

  if (!reservations) {
    return error ? (
      <p role="alert" className="py-16 text-center text-sm text-red-600">
        Rezervasyonlar yüklenemedi: {error.message}
      </p>
    ) : (
      <p className="py-16 text-center text-sm text-slate-500">Rezervasyonlar yükleniyor…</p>
    )
  }

  if (reservations.length === 0) {
    return (
      <div className="py-16 text-center">
        <p className="mb-4 text-slate-600">Henüz rezervasyonun yok.</p>
        <button type="button" onClick={onBrowse} className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700">
          Koltuk seç
        </button>
      </div>
    )
  }

  // Aktif tutmalar üstte (zaman baskısı olan tek şey onlar), sonra yeniden eskiye.
  const sorted = [...reservations].sort((a, b) => {
    const activeA = effectiveState(a, now) === 'active' ? 0 : 1
    const activeB = effectiveState(b, now) === 'active' ? 0 : 1
    return activeA - activeB || Date.parse(b.createdAt) - Date.parse(a.createdAt)
  })

  return (
    <section className="space-y-3" aria-label="Rezervasyonlarım">
      <h1 className="text-xl font-semibold">Rezervasyonlarım</h1>
      <ul className="space-y-3">
        {sorted.map((r) => {
          const state = effectiveState(r, now)
          if (state === 'active') {
            return <HoldCard key={r.id} reservation={r} onExpire={onChanged} onConfirmed={onChanged} />
          }
          const badge = BADGE[state]
          return (
            <li key={r.id} className="flex items-center justify-between gap-3 rounded-xl border border-slate-200 bg-white p-4">
              <div>
                <p className="font-semibold">
                  {r.seatLabel} <span className="font-normal text-slate-500">· {eventName(r.eventId)}</span>
                </p>
                <p className="text-xs text-slate-500">
                  {state === 'confirmed' && r.confirmedAt
                    ? `Satın alma: ${formatDateTime(r.confirmedAt)}`
                    : `Tutulma: ${formatDateTime(r.createdAt)}`}
                </p>
              </div>
              <span className={`rounded-full px-2.5 py-1 text-xs font-medium ${badge.className}`}>{badge.text}</span>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
