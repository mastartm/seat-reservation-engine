using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SeatReservation.Api.Tests.Support;

namespace SeatReservation.Api.Tests;

// Her test kendi factory'sini (ve sahte saatini) alır: saat ilerletilince sonradan üretilen JWT'nin 'nbf' değeri
// gerçek saatin ilerisinde kalır ve doğrulama 401 verir. İzolasyon bu yüzden sınıf fixture'ı yerine test başına.
public sealed class HoldApiTests : IDisposable
{
    private readonly ApiFactory factory = new();

    public void Dispose() => factory.Dispose();

    private async Task<(Guid SeatId, HttpClient First, HttpClient Second)> ArrangeAsync()
    {
        var seatId = (await factory.CreateEventAsync()).Single();
        var (_, t1) = await factory.CreateUserAsync();
        var (_, t2) = await factory.CreateUserAsync();
        return (seatId, factory.CreateClientWithToken(t1), factory.CreateClientWithToken(t2));
    }

    [Fact]
    public async Task Hold_succeeds_and_expires_in_ten_minutes()
    {
        var (seatId, first, _) = await ArrangeAsync();

        var response = await first.PostAsync($"/api/seats/{seatId}/hold", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Held", body.GetProperty("status").GetString());
        Assert.Equal(factory.Time.GetUtcNow().AddMinutes(10), body.GetProperty("expiresAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Second_hold_on_same_seat_is_rejected()
    {
        var (seatId, first, second) = await ArrangeAsync();
        await first.PostAsync($"/api/seats/{seatId}/hold", null);

        var response = await second.PostAsync($"/api/seats/{seatId}/hold", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Holder_cannot_hold_same_seat_twice()
    {
        var (seatId, first, _) = await ArrangeAsync();
        await first.PostAsync($"/api/seats/{seatId}/hold", null);

        var response = await first.PostAsync($"/api/seats/{seatId}/hold", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Seat_map_shows_held_seat_until_expiry_then_available_again()
    {
        var seatId = (await factory.CreateEventAsync(1, 2)).First();
        var (_, token) = await factory.CreateUserAsync();
        var client = factory.CreateClientWithToken(token);
        var eventId = await EventIdOfAsync(seatId);
        await client.PostAsync($"/api/seats/{seatId}/hold", null);

        Assert.Equal("Held", await StatusOfAsync(eventId, seatId));

        factory.Time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal("Available", await StatusOfAsync(eventId, seatId));
    }

    [Fact]
    public async Task Another_user_can_take_the_seat_after_hold_expired_but_not_before()
    {
        var (seatId, first, second) = await ArrangeAsync();
        await first.PostAsync($"/api/seats/{seatId}/hold", null);

        factory.Time.Advance(TimeSpan.FromMinutes(9) + TimeSpan.FromSeconds(59));
        Assert.Equal(HttpStatusCode.Conflict, (await second.PostAsync($"/api/seats/{seatId}/hold", null)).StatusCode);

        factory.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(HttpStatusCode.Created, (await second.PostAsync($"/api/seats/{seatId}/hold", null)).StatusCode);
    }

    [Fact]
    public async Task Hold_requires_authentication()
    {
        var seatId = (await factory.CreateEventAsync()).Single();

        var response = await factory.CreateClient().PostAsync($"/api/seats/{seatId}/hold", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Hold_unknown_seat_is_404()
    {
        var (_, first, _) = await ArrangeAsync();

        var response = await first.PostAsync($"/api/seats/{Guid.NewGuid()}/hold", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Guid> EventIdOfAsync(Guid seatId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeatReservation.Infrastructure.Persistence.AppDbContext>();
        return (await db.Seats.FindAsync(seatId))!.EventId;
    }

    private async Task<string?> StatusOfAsync(Guid eventId, Guid seatId)
    {
        var seats = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/events/{eventId}/seats");
        return seats.EnumerateArray().Single(s => s.GetProperty("id").GetGuid() == seatId).GetProperty("status").GetString();
    }
}
