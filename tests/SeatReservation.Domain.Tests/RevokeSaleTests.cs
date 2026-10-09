using SeatReservation.Domain.Entities;
using SeatReservation.Domain.Enums;

namespace SeatReservation.Domain.Tests;

/// <summary>Demo temizliğinin kullandığı sistem geri alması: yalnızca onaylı satın alma geri alınır.</summary>
public class RevokeSaleTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan TenMinutes = TimeSpan.FromMinutes(10);

    private static (Seat Seat, Reservation Reservation) Sold()
    {
        var seat = Event.Create("Konser", Now.AddDays(7), 1, 1).Seats.Single();
        var userId = Guid.NewGuid();
        var reservation = seat.Hold(userId, Now, TenMinutes);
        reservation.Confirm(userId, Now.AddMinutes(1));
        return (seat, reservation);
    }

    [Fact]
    public void Confirmed_sale_is_revoked_and_seat_becomes_available()
    {
        var (seat, reservation) = Sold();

        Assert.True(reservation.RevokeSale());

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(SeatStatus.Available, seat.Status);
        Assert.Null(seat.ActiveReservationId);
    }

    [Fact]
    public void Revoked_seat_can_be_held_again()
    {
        var (seat, reservation) = Sold();
        reservation.RevokeSale();

        var second = seat.Hold(Guid.NewGuid(), Now.AddMinutes(2), TenMinutes);

        Assert.Equal(SeatStatus.Held, seat.Status);
        Assert.Equal(second.Id, seat.ActiveReservationId);
    }

    [Fact]
    public void Held_reservation_is_not_touched()
    {
        var seat = Event.Create("Konser", Now.AddDays(7), 1, 1).Seats.Single();
        var reservation = seat.Hold(Guid.NewGuid(), Now, TenMinutes);

        Assert.False(reservation.RevokeSale());

        Assert.Equal(ReservationStatus.Held, reservation.Status);
        Assert.Equal(SeatStatus.Held, seat.Status);
    }

    [Fact]
    public void Revoking_twice_does_nothing_the_second_time()
    {
        var (_, reservation) = Sold();
        reservation.RevokeSale();

        Assert.False(reservation.RevokeSale());
    }
}
