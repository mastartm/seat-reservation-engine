using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SeatReservation.Api.Tests.Support;

namespace SeatReservation.Api.Tests;

// Saat ilerletildiği için test başına factory (bkz. HoldApiTests'teki açıklama).
public sealed class ConfirmApiTests : IDisposable
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
    public async Task Owner_confirms_and_seat_becomes_sold_for_everyone()
    {
        var (reservationId, seatId, owner, other) = await HoldAsync();

        var response = await owner.PostAsync($"/api/reservations/{reservationId}/confirm", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Confirmed", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        // Satılmış koltuk, süre ne kadar geçerse geçsin tekrar tutulamaz.
        factory.Time.Advance(TimeSpan.FromHours(1));
        Assert.Equal(HttpStatusCode.Conflict, (await other.PostAsync($"/api/seats/{seatId}/hold", null)).StatusCode);
    }

    [Fact]
    public async Task Only_the_holder_can_confirm()
    {
        var (reservationId, _, _, other) = await HoldAsync();

        var response = await other.PostAsync($"/api/reservations/{reservationId}/confirm", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_after_expiry_is_rejected()
    {
        var (reservationId, _, owner, _) = await HoldAsync();
        factory.Time.Advance(TimeSpan.FromMinutes(10));

        var response = await owner.PostAsync($"/api/reservations/{reservationId}/confirm", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_twice_is_conflict()
    {
        var (reservationId, _, owner, _) = await HoldAsync();
        await owner.PostAsync($"/api/reservations/{reservationId}/confirm", null);

        var again = await owner.PostAsync($"/api/reservations/{reservationId}/confirm", null);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Confirm_unknown_reservation_is_404_and_anonymous_is_401()
    {
        var (_, _, owner, _) = await HoldAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsync($"/api/reservations/{Guid.NewGuid()}/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsync($"/api/reservations/{Guid.NewGuid()}/confirm", null)).StatusCode);
    }

    [Fact]
    public async Task Mine_lists_only_own_reservations()
    {
        var (reservationId, _, owner, other) = await HoldAsync();

        var mine = await owner.GetFromJsonAsync<JsonElement>("/api/reservations/mine");
        var theirs = await other.GetFromJsonAsync<JsonElement>("/api/reservations/mine");

        Assert.Equal(reservationId, Assert.Single(mine.EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(0, theirs.GetArrayLength());
    }
}
