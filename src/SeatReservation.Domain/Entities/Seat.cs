using SeatReservation.Domain.Common;
using SeatReservation.Domain.Enums;

namespace SeatReservation.Domain.Entities;

/// <summary>
/// Eş zamanlılığın düğüm noktası. Durum geçişlerinin tamamı bu sınıftan geçer ve her geçiş
/// <see cref="RowVersion"/>'ı değiştirir; iki istek aynı koltuğu aynı anda değiştirmeye çalışırsa
/// veritabanı ikincisini reddeder (optimistic concurrency).
/// </summary>
public sealed class Seat
{
    private Seat() { }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public SeatStatus Status { get; private set; }

    /// <summary>Koltuğu şu an tutan/satın alan rezervasyon. Boşsa null.</summary>
    public Guid? ActiveReservationId { get; private set; }

    /// <summary>Tutma bitiş anı. Yalnızca <see cref="SeatStatus.Held"/> iken dolu.</summary>
    public DateTimeOffset? HoldExpiresAt { get; private set; }

    /// <summary>Veritabanı tarafından her UPDATE'te değiştirilen eş zamanlılık jetonu.</summary>
    public byte[] RowVersion { get; private set; } = [];

    internal static Seat Create(Guid eventId, string label) =>
        new() { Id = Guid.NewGuid(), EventId = eventId, Label = label, Status = SeatStatus.Available };

    /// <summary>
    /// Süresi dolmuş bir tutma, arka plan servisi henüz temizlememiş olsa bile "boş" sayılır.
    /// Böylece kullanıcılar temizlik periyodunu (örn. 30 sn) beklemek zorunda kalmaz.
    /// </summary>
    public SeatStatus StatusAt(DateTimeOffset now) =>
        Status == SeatStatus.Held && HoldExpiresAt <= now ? SeatStatus.Available : Status;

    /// <summary>Koltuğu <paramref name="userId"/> adına <paramref name="duration"/> süreyle tutar.</summary>
    public Reservation Hold(Guid userId, DateTimeOffset now, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            throw new DomainValidationException("Tutma süresi pozitif olmalı.");
        if (StatusAt(now) != SeatStatus.Available)
            throw new SeatNotAvailableException($"{Label} koltuğu müsait değil.");

        var reservation = Reservation.CreateHold(this, userId, now, duration);
        Status = SeatStatus.Held;
        ActiveReservationId = reservation.Id;
        HoldExpiresAt = reservation.ExpiresAt;
        return reservation;
    }

    // Aşağıdaki geçişler yalnızca Reservation üzerinden çağrılır: "kim" ve "ne zaman" kontrolleri
    // orada yapılır, burada sadece koltuğun kendi durum makinesi korunur.

    internal void ConfirmSale(Guid reservationId)
    {
        // Tutma süresi dolup başkası koltuğu aldıysa, eski rezervasyon artık aktif değildir.
        if (Status != SeatStatus.Held || ActiveReservationId != reservationId)
            throw new SeatNotAvailableException($"{Label} koltuğu bu rezervasyon için tutulmuyor.");

        Status = SeatStatus.Sold;
        HoldExpiresAt = null;
    }

    internal void ReleaseHold(Guid reservationId)
    {
        // Koltuk bu arada başkasına geçtiyse (veya satıldıysa) dokunma.
        if (Status != SeatStatus.Held || ActiveReservationId != reservationId) return;

        Status = SeatStatus.Available;
        ActiveReservationId = null;
        HoldExpiresAt = null;
    }
}
