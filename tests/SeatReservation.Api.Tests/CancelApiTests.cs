using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SeatReservation.Api.Tests.Support;

namespace SeatReservation.Api.Tests;

// Saat ilerletildiği için test başına factory (bkz. HoldApiTests'teki açıklama).
public sealed class CancelApiTests : IDisposable
{
    private readonly ApiFactory factory = new();

    public void Dispose() => factory.Dispose();

    private async Task<(Guid ReservationId, Guid SeatId, HttpClient Owner, HttpClient Other)> HoldAsync()
    {
        var seatId = (await factory.CreateEventAsync()).Single();
        var owner = factory.CreateClientWithToken((await factory.CreateUserAsync()).Token);
        var other = factory.CreateClientWithToken((await factory.CreateUserAsync()).Token);
        var hold = await owner.PostAsync($"/api/seats/{seatId}/hold", null);
        var id = (await hold.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        return (id, seatId, owner, other);
    }

    [Fact]
    public async Task Owner_cancels_and_someone_else_can_hold_the_seat_immediately()
    {
        var (reservationId, seatId, owner, other) = await HoldAsync();

        var response = await owner.PostAsync($"/api/reservations/{reservationId}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Cancelled", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        // Süre dolmasını beklemeden (saat ilerletilmedi) koltuk yeniden tutulabilir.
        Assert.Equal(HttpStatusCode.Created, (await other.PostAsync($"/api/seats/{seatId}/hold", null)).StatusCode);
    }

    [Fact]
    public async Task Only_the_holder_can_cancel_and_the_hold_survives_the_attempt()
    {
        var (reservationId, seatId, owner, other) = await HoldAsync();

        var response = await other.PostAsync($"/api/reservations/{reservationId}/cancel", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await other.PostAsync($"/api/seats/{seatId}/hold", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"/api/reservations/{reservationId}/confirm", null)).StatusCode);
    }

    [Fact]
    public async Task Confirmed_reservation_cannot_be_cancelled()
    {
        var (reservationId, seatId, owner, other) = await HoldAsync();
        await owner.PostAsync($"/api/reservations/{reservationId}/confirm", null);

        var response = await owner.PostAsync($"/api/reservations/{reservationId}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        // Koltuk satılmış kalır.
        Assert.Equal(HttpStatusCode.Conflict, (await other.PostAsync($"/api/seats/{seatId}/hold", null)).StatusCode);
    }

    [Fact]
    public async Task Cancel_twice_is_conflict()
    {
        var (reservationId, _, owner, _) = await HoldAsync();
        await owner.PostAsync($"/api/reservations/{reservationId}/cancel", null);

        var again = await owner.PostAsync($"/api/reservations/{reservationId}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Cancel_after_expiry_is_conflict()
    {
        var (reservationId, _, owner, _) = await HoldAsync();
        factory.Time.Advance(TimeSpan.FromMinutes(10));

        var response = await owner.PostAsync($"/api/reservations/{reservationId}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Cancelled_reservation_cannot_be_confirmed()
    {
        var (reservationId, _, owner, _) = await HoldAsync();
        await owner.PostAsync($"/api/reservations/{reservationId}/cancel", null);

        var response = await owner.PostAsync($"/api/reservations/{reservationId}/confirm", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_unknown_reservation_is_404_and_anonymous_is_401()
    {
        var (_, _, owner, _) = await HoldAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsync($"/api/reservations/{Guid.NewGuid()}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsync($"/api/reservations/{Guid.NewGuid()}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task Mine_shows_the_reservation_as_cancelled()
    {
        var (reservationId, _, owner, _) = await HoldAsync();
        await owner.PostAsync($"/api/reservations/{reservationId}/cancel", null);

        var mine = await owner.GetFromJsonAsync<JsonElement>("/api/reservations/mine");

        Assert.Equal("Cancelled", Assert.Single(mine.EnumerateArray()).GetProperty("status").GetString());
    }
}
