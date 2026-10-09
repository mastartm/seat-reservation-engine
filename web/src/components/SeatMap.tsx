import type { ReservationView, SeatView } from '../api/types'
import { LEGEND, seatDisplayState, type SeatDisplayState } from '../lib/seatState'

const STYLE: Record<SeatDisplayState, { label: string; className: string }> = {
  available: { label: 'boş', className: 'bg-emerald-100 text-emerald-900 border-emerald-300 hover:bg-emerald-200 cursor-pointer' },
  held: { label: 'başkası tutuyor', className: 'bg-amber-100 text-amber-800 border-amber-300 cursor-not-allowed' },
  'mine-held': { label: 'senin için tutuldu', className: 'bg-indigo-600 text-white border-indigo-700 ring-2 ring-indigo-300' },
  'mine-sold': { label: 'senin koltuğun', className: 'bg-indigo-900 text-white border-indigo-950' },
  sold: { label: 'satıldı', className: 'bg-slate-300 text-slate-500 border-slate-400 cursor-not-allowed line-through' },
}

interface Props {
  seats: SeatView[]
  mine: ReadonlyMap<string, ReservationView>
  onSelect?: (seat: SeatView) => void
  /** İsteği süren koltuk: çift tıklamayı engeller. */
  busySeatId?: string | null
}

export function SeatMap({ seats, mine, onSelect, busySeatId = null }: Props) {
  // Satır harfine göre grupla, satır içinde numaraya göre sırala ("A10", "A2"den sonra gelmeli).
  const rows = new Map<string, SeatView[]>()
  for (const seat of seats) {
    const row = seat.label[0]
    rows.set(row, [...(rows.get(row) ?? []), seat])
  }
  const orderedRows = [...rows.entries()].sort(([a], [b]) => a.localeCompare(b))

  return (
    <div className="overflow-x-auto pb-2">
      <div
        className="mx-auto mb-6 w-3/4 min-w-64 rounded-b-full bg-slate-800 py-1 text-center text-xs font-medium tracking-widest text-slate-200"
        aria-hidden="true"
      >
        SAHNE
      </div>
      <div className="flex flex-col items-center gap-1.5" role="group" aria-label="Koltuk haritası">
        {orderedRows.map(([row, rowSeats]) => (
          <div key={row} className="flex items-center gap-1.5">
            <span className="w-5 text-center text-xs font-semibold text-slate-500" aria-hidden="true">
              {row}
            </span>
            {rowSeats
              .sort((a, b) => Number(a.label.slice(1)) - Number(b.label.slice(1)))
              .map((seat) => {
                const state = seatDisplayState(seat, mine)
                const { label, className } = STYLE[state]
                const clickable = state === 'available' && onSelect !== undefined && busySeatId === null
                return (
                  <button
                    key={seat.id}
                    type="button"
                    data-state={state}
                    aria-label={`${seat.label}, ${label}`}
                    disabled={!clickable}
                    onClick={() => onSelect?.(seat)}
                    className={`h-8 w-8 rounded-t-lg border text-[11px] font-medium transition-colors disabled:cursor-not-allowed sm:h-9 sm:w-9 ${className} ${
                      busySeatId === seat.id ? 'animate-pulse' : ''
                    }`}
                  >
                    {seat.label.slice(1)}
                  </button>
                )
              })}
          </div>
        ))}
      </div>
    </div>
  )
}

export function SeatLegend() {
  return (
    <ul className="flex flex-wrap justify-center gap-x-4 gap-y-1 text-xs text-slate-600">
      {LEGEND.map(({ state, text }) => (
        <li key={state} className="flex items-center gap-1.5">
          <span className={`inline-block h-4 w-4 rounded-t border ${STYLE[state].className.replace(/cursor-\S+|hover:\S+|line-through/g, '')}`} />
          {text}
        </li>
      ))}
    </ul>
  )
}
