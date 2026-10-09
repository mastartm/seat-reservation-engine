using SeatReservation.Domain.Common;
using SeatReservation.Domain.Entities;
using SeatReservation.Domain.Enums;

namespace SeatReservation.Domain.Tests;

public class SeatTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan TenMinutes = TimeSpan.FromMinutes(10);

    private static Seat NewSeat() => Event.Create("Konser", Now.AddDays(7), 1, 1).Seats.Single();

    [Fact]
    public void Hold_available_seat_marks_it_held_until_expiry()
    {
        var seat = NewSeat();
        var userId = Guid.NewGuid();

        var reservation = seat.Hold(userId, Now, TenMinutes);

        Assert.Equal(SeatStatus.Held, seat.Status);
        Assert.Equal(reservation.Id, seat.ActiveReservationId);
        Assert.Equal(Now + TenMinutes, seat.HoldExpiresAt);
        Assert.Equal(userId, reservation.UserId);
        Assert.Equal(ReservationStatus.Held, reservation.Status);
    }

    [Fact]
    public void Second_hold_while_first_is_active_is_rejected()
    {
        var seat = NewSeat();
        seat.Hold(Guid.NewGuid(), Now, TenMinutes);

        Assert.Throws<SeatNotAvailableException>(() =>
            seat.Hold(Guid.NewGuid(), Now.AddMinutes(5), TenMinutes));
    }

    [Fact]
    public void Hold_is_allowed_again_once_previous_hold_has_expired()
    {
        var seat = NewSeat();
        var first = seat.Hold(Guid.NewGuid(), Now, TenMinutes);

        var second = seat.Hold(Guid.NewGuid(), Now + TenMinutes, TenMinutes);

        Assert.Equal(second.Id, seat.ActiveReservationId);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void StatusAt_reports_expired_hold_as_available_without_mutating()
    {
        var seat = NewSeat();
        seat.Hold(Guid.NewGuid(), Now, TenMinutes);

        Assert.Equal(SeatStatus.Held, seat.StatusAt(Now.AddMinutes(9)));
        Assert.Equal(SeatStatus.Available, seat.StatusAt(Now.AddMinutes(10)));
        Assert.Equal(SeatStatus.Held, seat.Status);
    }

    [Fact]
    public void Hold_rejects_non_positive_duration()
    {
        Assert.Throws<DomainValidationException>(() => NewSeat().Hold(Guid.NewGuid(), Now, TimeSpan.Zero));
    }

    [Fact]
    public void Sold_seat_cannot_be_held_even_long_after()
    {
        var seat = NewSeat();
        var userId = Guid.NewGuid();
        seat.Hold(userId, Now, TenMinutes).Confirm(userId, Now.AddMinutes(1));

        Assert.Throws<SeatNotAvailableException>(() =>
            seat.Hold(Guid.NewGuid(), Now.AddDays(1), TenMinutes));
    }
}
