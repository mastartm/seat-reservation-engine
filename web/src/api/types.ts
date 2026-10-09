// Sunucu sözleşmesinin aynası (src/SeatReservation.Api + Application). Enum'lar JSON'da ad olarak gelir.
export type SeatStatus = 'Available' | 'Held' | 'Sold'
export type ReservationStatus = 'Held' | 'Confirmed' | 'Expired'
export type UserRole = 'User' | 'Admin'

export interface EventSummary {
  id: string
  name: string
  startsAt: string
}

export interface SeatView {
  id: string
  label: string
  status: SeatStatus
}

export interface ReservationView {
  id: string
  seatId: string
  eventId: string
  seatLabel: string
  status: ReservationStatus
  createdAt: string
  expiresAt: string
  confirmedAt: string | null
}

export interface AuthResult {
  token: string
  expiresAt: string
  userId: string
  email: string
  role: UserRole
}
