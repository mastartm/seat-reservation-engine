namespace SeatReservation.Application.Reservations;

public sealed class ReservationOptions
{
    public const string SectionName = "Reservation";

    /// <summary>Bir koltuğun onaylanmadan tutulabileceği süre.</summary>
    public TimeSpan HoldDuration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Süre dolum taramasının sıklığı. Kullanıcı bunu hissetmez: dolmuş tutmalar okuma/tutma anında zaten boş sayılır.</summary>
    public TimeSpan ExpirySweepInterval { get; set; } = TimeSpan.FromSeconds(30);
}
