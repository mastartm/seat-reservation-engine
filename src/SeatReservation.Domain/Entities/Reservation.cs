using SeatReservation.Domain.Common;
using SeatReservation.Domain.Enums;

namespace SeatReservation.Domain.Entities;

public sealed class Reservation
{
    private Reservation() { }

    public Guid Id { get; private set; }
    public Guid SeatId { get; private set; }
    public Seat Seat { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }

    internal static Reservation CreateHold(Seat seat, Guid userId, DateTimeOffset now, TimeSpan duration) =>
        new()
        {
            Id = Guid.NewGuid(),
            Seat = seat,
            SeatId = seat.Id,
            UserId = userId,
            Status = ReservationStatus.Held,
            CreatedAt = now,
            ExpiresAt = now + duration,
        };

    /// <summary>Satın almayı onaylar. Yalnızca tutmanın sahibi, süre dolmadan onaylayabilir.</summary>
    public void Confirm(Guid userId, DateTimeOffset now)
    {
        // Sahiplik kontrolü ilk sırada: yabancıya rezervasyonun süre/durum bilgisi sızmasın.
        if (userId != UserId)
            throw new NotHoldOwnerException("Bu rezervasyonu yalnızca tutan kullanıcı onaylayabilir.");
        if (Status == ReservationStatus.Confirmed)
            throw new InvalidStateTransitionException("Rezervasyon zaten onaylanmış.");
        if (Status == ReservationStatus.Expired || now >= ExpiresAt)
            throw new HoldExpiredException("Tutma süresi dolmuş.");

        Seat.ConfirmSale(Id);
        Status = ReservationStatus.Confirmed;
        ConfirmedAt = now;
    }

    /// <summary>
    /// Süresi dolmuş tutmayı kapatır ve koltuğu serbest bırakır.
    /// Süresi dolmamış ya da zaten sonuçlanmış rezervasyona dokunmaz; false döner.
    /// </summary>
    public bool Expire(DateTimeOffset now)
    {
        if (Status != ReservationStatus.Held || now < ExpiresAt) return false;

        Status = ReservationStatus.Expired;
        Seat.ReleaseHold(Id);
        return true;
    }
}
