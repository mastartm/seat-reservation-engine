namespace SeatReservation.Application.Reservations;

public sealed class ReservationOptions
{
    public const string SectionName = "Reservation";

    /// <summary>Bir koltuğun onaylanmadan tutulabileceği süre.</summary>
    public TimeSpan HoldDuration { get; set; } = TimeSpan.FromMinutes(10);
}
