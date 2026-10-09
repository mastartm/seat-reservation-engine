using SeatReservation.Domain.Common;
using SeatReservation.Domain.Entities;

namespace SeatReservation.Domain.Tests;

public class EventTests
{
    private static readonly DateTimeOffset Later = new(2030, 6, 1, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_generates_seat_grid_with_labels()
    {
        var ev = Event.Create("  Konser ", Later, rows: 2, seatsPerRow: 3);

        Assert.Equal("Konser", ev.Name);
        Assert.Equal(["A1", "A2", "A3", "B1", "B2", "B3"], ev.Seats.Select(s => s.Label));
        Assert.All(ev.Seats, s => Assert.Equal(ev.Id, s.EventId));
    }

    [Theory]
    [InlineData("", 1, 1)]
    [InlineData("  ", 1, 1)]
    [InlineData("X", 0, 1)]
    [InlineData("X", 27, 1)]
    [InlineData("X", 1, 0)]
    [InlineData("X", 1, 101)]
    public void Create_rejects_invalid_input(string name, int rows, int seatsPerRow)
    {
        Assert.Throws<DomainValidationException>(() => Event.Create(name, Later, rows, seatsPerRow));
    }
}
