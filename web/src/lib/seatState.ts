import type { ReservationView, SeatView } from '../api/types'

/** Ekranda gösterilen durum: sunucunun 3 durumuna "benim" ayrımı eklenir (kendi tuttuğum / satın aldığım). */
export type SeatDisplayState = 'available' | 'held' | 'mine-held' | 'mine-sold' | 'sold'

/**
 * `mine`: oturumdaki kullanıcının aktif (tutulu veya onaylı) rezervasyonları, koltuk kimliğine göre.
 * Sunucu koltuk haritasında kimin tuttuğunu söylemez (başkalarının bilgisi sızmasın); "benim" bilgisi
 * yalnızca kendi rezervasyonlarımdan türetilir.
 */
export function seatDisplayState(seat: SeatView, mine: ReadonlyMap<string, ReservationView>): SeatDisplayState {
  const own = mine.get(seat.id)
  if (own?.status === 'Confirmed' && seat.status === 'Sold') return 'mine-sold'
  if (own?.status === 'Held' && seat.status === 'Held') return 'mine-held'
  if (seat.status === 'Sold') return 'sold'
  if (seat.status === 'Held') return 'held'
  return 'available'
}

export const LEGEND: { state: SeatDisplayState; text: string }[] = [
  { state: 'available', text: 'Boş' },
  { state: 'held', text: 'Başkası tutuyor' },
  { state: 'mine-held', text: 'Senin tuttuğun' },
  { state: 'mine-sold', text: 'Satın aldığın' },
  { state: 'sold', text: 'Satıldı' },
]
