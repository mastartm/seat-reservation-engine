namespace SeatReservation.Api.Tests.Support;

/// <summary>Elle ilerletilen saat: 10 dakikalık tutma süresini gerçekten beklemeden sınamak için.</summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
