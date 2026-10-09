using SeatReservation.Domain.Common;

namespace SeatReservation.Domain.Entities;

public sealed class Event
{
    public const int MaxRows = 26; // satır etiketi A–Z
    public const int MaxSeatsPerRow = 100;

    private readonly List<Seat> _seats = [];

    private Event() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset StartsAt { get; private set; }
    public IReadOnlyCollection<Seat> Seats => _seats;

    /// <summary>Etkinliği ve satır/sütun ızgarasından koltuklarını üretir (A1, A2, ... B1, ...).</summary>
    public static Event Create(string name, DateTimeOffset startsAt, int rows, int seatsPerRow)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainValidationException("Etkinlik adı gerekli.");
        if (rows is < 1 or > MaxRows)
            throw new DomainValidationException($"Satır sayısı 1–{MaxRows} arasında olmalı.");
        if (seatsPerRow is < 1 or > MaxSeatsPerRow)
            throw new DomainValidationException($"Satır başına koltuk 1–{MaxSeatsPerRow} arasında olmalı.");

        var ev = new Event { Id = Guid.NewGuid(), Name = name.Trim(), StartsAt = startsAt };
        for (var r = 0; r < rows; r++)
        for (var n = 1; n <= seatsPerRow; n++)
            ev._seats.Add(Seat.Create(ev.Id, $"{(char)('A' + r)}{n}"));
        return ev;
    }
}
