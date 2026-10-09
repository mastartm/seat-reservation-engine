namespace SeatReservation.Application.Reservations;

public sealed class ReservationOptions
{
    public const string SectionName = "Reservation";

    /// <summary>Bir koltuğun onaylanmadan tutulabileceği süre.</summary>
    public TimeSpan HoldDuration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Bir kullanıcının aynı anda sahip olabileceği aktif koltuk sayısı (süresi dolmamış tutma + onaylı satın alma).
    /// Tek kişinin tüm salonu tutmasını engeller. Sıfır: sınırsız. Kontrol-sonra-yaz olduğundan aynı kullanıcının
    /// paralel istekleri sınırı birkaç koltuk aşabilir; koltukların kendisi yine tam bir kişiye gider (RowVersion).
    /// </summary>
    public int MaxActiveReservationsPerUser { get; set; } = 6;

    /// <summary>Süre dolum taramasının sıklığı. Kullanıcı bunu hissetmez: dolmuş tutmalar okuma/tutma anında zaten boş sayılır.</summary>
    public TimeSpan ExpirySweepInterval { get; set; } = TimeSpan.FromSeconds(30);
}
