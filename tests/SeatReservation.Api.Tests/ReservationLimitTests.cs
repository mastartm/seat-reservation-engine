using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SeatReservation.Api.Tests.Support;
using SeatReservation.Application.Reservations;

namespace SeatReservation.Api.Tests;

/// <summary>Kullanıcı başına aktif koltuk sınırı: tek kişi tüm salonu tutamasın.</summary>
public sealed class ReservationLimitTests : IDisposable
{
    private readonly ApiFactory factory = new();

    public void Dispose() => factory.Dispose();

    private void SetLimit(int max) =>
        factory.Services.GetRequiredService<IOptions<ReservationOptions>>().Value.MaxActiveReservationsPerUser = max;

    private static async Task<(HttpStatusCode Status, Guid ReservationId)> HoldAsync(HttpClient client, Guid seatId)
    {
        var response = await client.PostAsync($"/api/seats/{seatId}/hold", null);
        var id = response.StatusCode == HttpStatusCode.Created
            ? (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid()
            : Guid.Empty;
        return (response.StatusCode, id);
    }

    private async Task<HttpClient> NewClientAsync() => factory.CreateClientWithToken((await factory.CreateUserAsync()).Token);

    [Fact]
    public async Task Holding_beyond_the_limit_is_conflict_with_a_helpful_message()
    {
        SetLimit(2);
        var seats = await factory.CreateEventAsync(1, 4);
        var client = await NewClientAsync();

        Assert.Equal(HttpStatusCode.Created, (await HoldAsync(client, seats[0])).Status);
        Assert.Equal(HttpStatusCode.Created, (await HoldAsync(client, seats[1])).Status);
        var third = await client.PostAsync($"/api/seats/{seats[2]}/hold", null);

        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);
        var detail = (await third.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();
        Assert.Contains("en fazla 2", detail);
    }

    [Fact]
    public async Task Cancelling_a_hold_frees_a_slot()
    {
        SetLimit(1);
        var seats = await factory.CreateEventAsync(1, 3);
        var client = await NewClientAsync();
        var (_, reservationId) = await HoldAsync(client, seats[0]);
        Assert.Equal(HttpStatusCode.Conflict, (await HoldAsync(client, seats[1])).Status);

        await client.PostAsync($"/api/reservations/{reservationId}/cancel", null);

        Assert.Equal(HttpStatusCode.Created, (await HoldAsync(client, seats[1])).Status);
    }

    [Fact]
    public async Task Confirmed_purchases_count_toward_the_limit()
    {
        SetLimit(2);
        var seats = await factory.CreateEventAsync(1, 4);
        var client = await NewClientAsync();
        foreach (var seat in seats.Take(2))
        {
            var (_, id) = await HoldAsync(client, seat);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/reservations/{id}/confirm", null)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Conflict, (await HoldAsync(client, seats[2])).Status);
    }

    [Fact]
    public async Task Expired_holds_do_not_count()
    {
        SetLimit(2);
        var seats = await factory.CreateEventAsync(1, 4);
        var client = await NewClientAsync();
        await HoldAsync(client, seats[0]);
        await HoldAsync(client, seats[1]);
        factory.Time.Advance(TimeSpan.FromMinutes(11));

        Assert.Equal(HttpStatusCode.Created, (await HoldAsync(client, seats[2])).Status);
    }

    [Fact]
    public async Task Limit_is_per_user()
    {
        SetLimit(1);
        var seats = await factory.CreateEventAsync(1, 3);
        var first = await NewClientAsync();
        var second = await NewClientAsync();
        await HoldAsync(first, seats[0]);

        Assert.Equal(HttpStatusCode.Created, (await HoldAsync(second, seats[1])).Status);
    }

    [Fact]
    public async Task Zero_means_unlimited()
    {
        SetLimit(0);
        var seats = await factory.CreateEventAsync(1, 8);
        var client = await NewClientAsync();

        foreach (var seat in seats)
            Assert.Equal(HttpStatusCode.Created, (await HoldAsync(client, seat)).Status);
    }
}
