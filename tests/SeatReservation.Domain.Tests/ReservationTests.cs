using SeatReservation.Domain.Common;
using SeatReservation.Domain.Entities;
using SeatReservation.Domain.Enums;

namespace SeatReservation.Domain.Tests;

public class ReservationTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan TenMinutes = TimeSpan.FromMinutes(10);

    private static (Seat Seat, Reservation Reservation, Guid UserId) Held()
    {
        var seat = Event.Create("Konser", Now.AddDays(7), 1, 1).Seats.Single();
        var userId = Guid.NewGuid();
        return (seat, seat.Hold(userId, Now, TenMinutes), userId);
    }

    [Fact]
    public void Owner_can_confirm_before_expiry()
    {
        var (seat, reservation, userId) = Held();

        reservation.Confirm(userId, Now.AddMinutes(9));

        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Equal(SeatStatus.Sold, seat.Status);
        Assert.Equal(Now.AddMinutes(9), reservation.ConfirmedAt);
    }

    [Fact]
    public void Non_owner_cannot_confirm()
    {
        var (seat, reservation, _) = Held();

        Assert.Throws<NotHoldOwnerException>(() => reservation.Confirm(Guid.NewGuid(), Now.AddMinutes(1)));
        Assert.Equal(SeatStatus.Held, seat.Status);
    }

    [Fact]
    public void Cannot_confirm_after_expiry()
    {
        var (seat, reservation, userId) = Held();

        Assert.Throws<HoldExpiredException>(() => reservation.Confirm(userId, Now + TenMinutes));
        Assert.Equal(SeatStatus.Held, seat.Status);
    }

    [Fact]
    public void Cannot_confirm_twice()
    {
        var (_, reservation, userId) = Held();
        reservation.Confirm(userId, Now.AddMinutes(1));

        Assert.Throws<InvalidStateTransitionException>(() => reservation.Confirm(userId, Now.AddMinutes(2)));
    }

    [Fact]
    public void Stale_reservation_cannot_confirm_after_seat_was_taken_over()
    {
        var (seat, stale, staleOwner) = Held();
        // Süre dolmadan önce onaylamayı denemiş gibi: koltuk, sürenin dolmasından sonra başkasına geçer.
        seat.Hold(Guid.NewGuid(), Now + TenMinutes, TenMinutes);

        Assert.Throws<HoldExpiredException>(() => stale.Confirm(staleOwner, Now + TenMinutes));
    }

    [Fact]
    public void Expire_releases_the_seat_once_time_has_passed()
    {
        var (seat, reservation, _) = Held();

        Assert.False(reservation.Expire(Now.AddMinutes(9)));
        Assert.Equal(SeatStatus.Held, seat.Status);

        Assert.True(reservation.Expire(Now + TenMinutes));
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        Assert.Equal(SeatStatus.Available, seat.Status);
        Assert.Null(seat.ActiveReservationId);
    }

    [Fact]
    public void Expire_does_not_release_a_seat_already_taken_by_someone_else()
    {
        var (seat, stale, _) = Held();
        var second = seat.Hold(Guid.NewGuid(), Now + TenMinutes, TenMinutes);

        stale.Expire(Now + TenMinutes);

        Assert.Equal(SeatStatus.Held, seat.Status);
        Assert.Equal(second.Id, seat.ActiveReservationId);
    }

    [Fact]
    public void Expire_ignores_confirmed_reservations()
    {
        var (seat, reservation, userId) = Held();
        reservation.Confirm(userId, Now.AddMinutes(1));

        Assert.False(reservation.Expire(Now.AddHours(1)));
        Assert.Equal(SeatStatus.Sold, seat.Status);
    }
}
