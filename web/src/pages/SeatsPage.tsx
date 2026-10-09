import { useCallback, useMemo, useState } from 'react'
import { ApiError, api } from '../api/client'
import type { EventSummary, ReservationView, SeatView } from '../api/types'
import { HoldPanel } from '../components/HoldPanel'
import { SeatLegend, SeatMap } from '../components/SeatMap'
import { formatDateTime } from '../lib/format'
import { usePolled } from '../lib/usePolled'

// 3 sn: bir koltuğun "tutuldu"ya dönmesini kullanıcı birkaç saniye içinde görür; sorgu hafif (tek etkinliğin koltukları).
const SEAT_POLL_MS = 3000

interface Props {
  events: EventSummary[]
  /** Koltuk → geçerli rezervasyonum (harita "benim" renklerini buradan alır). */
  mine: ReadonlyMap<string, ReservationView>
  /** Sunucuda "Held" olan rezervasyonlarım. */
  holds: readonly ReservationView[]
  loggedIn: boolean
  onLoginRequired: () => void
  /** Hold/onay sonrası rezervasyon listesini tazelemek için. */
  onReservationsChanged: () => void
}

type Notice = { kind: 'success' | 'error'; text: string }

export function SeatsPage({ events, mine, holds, loggedIn, onLoginRequired, onReservationsChanged }: Props) {
  const [eventId, setEventId] = useState<string | null>(null)
  const [busySeatId, setBusySeatId] = useState<string | null>(null)
  const [notice, setNotice] = useState<Notice | null>(null)
  const selectedId = eventId ?? events[0]?.id ?? null

  const loadSeats = useCallback(() => api.listSeats(selectedId ?? ''), [selectedId])
  const seats = usePolled(loadSeats, SEAT_POLL_MS, selectedId !== null)

  async function hold(seat: SeatView) {
    if (!loggedIn) {
      onLoginRequired()
      return
    }
    setBusySeatId(seat.id)
    setNotice(null)
    try {
      const reservation = await api.holdSeat(seat.id)
      setNotice({ kind: 'success', text: `${reservation.seatLabel} 10 dakikalığına senin için tutuldu. Süre dolmadan onayla.` })
    } catch (e) {
      // 409: aynı anda başkası kazandı. Yeniden denemek anlamsız; güncel haritayı göstermek doğru yanıt.
      setNotice({ kind: 'error', text: e instanceof ApiError ? e.message : 'Koltuk tutulamadı.' })
    } finally {
      setBusySeatId(null)
      void seats.reload()
      onReservationsChanged()
    }
  }

  function confirmed(reservation: ReservationView) {
    setNotice({ kind: 'success', text: `${reservation.seatLabel} satın alındı. Rezervasyonlarım'dan görebilirsin.` })
    void seats.reload()
    onReservationsChanged()
  }

  const selectedEvent = useMemo(() => events.find((e) => e.id === selectedId), [events, selectedId])

  if (events.length === 0) {
    return <p className="py-16 text-center text-slate-500">Henüz etkinlik yok.</p>
  }

  return (
    <section className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <label htmlFor="event" className="mb-1 block text-xs font-medium uppercase tracking-wide text-slate-500">
            Etkinlik
          </label>
          <select
            id="event"
            value={selectedId ?? ''}
            onChange={(e) => setEventId(e.target.value)}
            className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm"
          >
            {events.map((ev) => (
              <option key={ev.id} value={ev.id}>
                {ev.name}
              </option>
            ))}
          </select>
        </div>
        {selectedEvent && <p className="text-sm text-slate-600">{formatDateTime(selectedEvent.startsAt)}</p>}
      </div>

      {notice && (
        <p
          role={notice.kind === 'error' ? 'alert' : 'status'}
          className={`rounded-lg px-3 py-2 text-sm ${notice.kind === 'error' ? 'bg-red-50 text-red-700' : 'bg-emerald-50 text-emerald-800'}`}
        >
          {notice.text}
        </p>
      )}

      {loggedIn && <HoldPanel holds={holds} onChanged={() => { void seats.reload(); onReservationsChanged() }} onConfirmed={confirmed} />}

      <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        {seats.error && !seats.data && (
          <p role="alert" className="py-10 text-center text-sm text-red-600">
            Koltuklar yüklenemedi: {seats.error.message}
          </p>
        )}
        {seats.loading && !seats.data && <p className="py-10 text-center text-sm text-slate-500">Koltuklar yükleniyor…</p>}
        {seats.data && (
          <div className="space-y-5">
            <SeatMap seats={seats.data} mine={mine} onSelect={hold} busySeatId={busySeatId} />
            {!loggedIn && <p className="text-center text-xs text-slate-500">Koltuk tutmak için giriş yapman gerekir.</p>}
            <SeatLegend />
            {seats.error && (
              <p role="status" className="text-center text-xs text-amber-700">
                Canlı güncelleme geçici olarak kesildi, yeniden deneniyor…
              </p>
            )}
          </div>
        )}
      </div>
    </section>
  )
}
