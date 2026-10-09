namespace SeatReservation.Domain.Enums;

public enum SeatStatus
{
    Available = 0,
    Held = 1,
    Sold = 2,
}

public enum ReservationStatus
{
    Held = 0,
    Confirmed = 1,
    Expired = 2,
}

public enum UserRole
{
    User = 0,
    Admin = 1,
}
