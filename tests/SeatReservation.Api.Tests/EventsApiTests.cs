using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SeatReservation.Api.Tests.Support;
using SeatReservation.Domain.Enums;

namespace SeatReservation.Api.Tests;

public class EventsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static object NewEvent(int rows = 2, int seatsPerRow = 3) =>
        new { name = "Konser", startsAt = DateTimeOffset.UtcNow.AddDays(30), rows, seatsPerRow };

    [Fact]
    public async Task Admin_creates_event_and_anyone_sees_its_seat_map()
    {
        var (_, adminToken) = await factory.CreateUserAsync(UserRole.Admin);

        var created = await factory.CreateClientWithToken(adminToken).PostAsJsonAsync("/api/events", NewEvent());

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var seats = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/events/{id}/seats");
        Assert.Equal(6, seats.GetArrayLength());
        Assert.Equal("A1", seats[0].GetProperty("label").GetString());
        Assert.Equal("B3", seats[5].GetProperty("label").GetString());
        Assert.All(seats.EnumerateArray(), s => Assert.Equal("Available", s.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task Regular_user_cannot_create_events()
    {
        var (_, userToken) = await factory.CreateUserAsync(UserRole.User);

        var response = await factory.CreateClientWithToken(userToken).PostAsJsonAsync("/api/events", NewEvent());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_cannot_create_events()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/events", NewEvent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_grid_is_rejected()
    {
        var (_, adminToken) = await factory.CreateUserAsync(UserRole.Admin);

        var response = await factory.CreateClientWithToken(adminToken).PostAsJsonAsync("/api/events", NewEvent(rows: 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Seats_of_unknown_event_is_404()
    {
        var response = await factory.CreateClient().GetAsync($"/api/events/{Guid.NewGuid()}/seats");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_returns_created_events()
    {
        var (_, adminToken) = await factory.CreateUserAsync(UserRole.Admin);
        await factory.CreateClientWithToken(adminToken).PostAsJsonAsync("/api/events", NewEvent(1, 1));

        var list = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/events");

        Assert.True(list.GetArrayLength() >= 1);
    }
}
