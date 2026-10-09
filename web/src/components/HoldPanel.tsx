import { useCallback, useState } from 'react'
import type { ReservationView } from '../api/types'
import { HoldCard } from './HoldCard'

interface Props {
  /** Sunucudaki durumu "Held" olan rezervasyonlarım. */
  holds: readonly ReservationView[]
  onChanged: () => void
  onConfirmed: (reservation: ReservationView) => void
}

/**
 * Aktif tutmalar + az önce süresi dolanlar. Süresi dolan kart, sunucu onu "Expired"a çevirip listeden
 * düşürse bile kullanıcı kapatana kadar kalır: koltuğun neden kaybolduğunu görsün.
 */
export function HoldPanel({ holds, onChanged, onConfirmed }: Props) {
  const [expired, setExpired] = useState<ReadonlyMap<string, ReservationView>>(new Map())

  const handleExpire = useCallback(
    (reservation: ReservationView) => {
      setExpired((prev) => new Map(prev).set(reservation.id, reservation))
      onChanged()
    },
    [onChanged],
  )
  const dismiss = useCallback(
    (reservation: ReservationView) =>
      setExpired((prev) => {
        const next = new Map(prev)
        next.delete(reservation.id)
        return next
      }),
    [],
  )

  const items = new Map<string, ReservationView>(expired)
  for (const h of holds) items.set(h.id, h)
  if (items.size === 0) return null

  return (
    <section aria-label="Tuttuğun koltuklar" className="space-y-3">
      <ul className="space-y-3">
        {[...items.values()].map((r) => (
          <HoldCard key={r.id} reservation={r} onExpire={handleExpire} onConfirmed={onConfirmed} onDismiss={dismiss} />
        ))}
      </ul>
    </section>
  )
}
