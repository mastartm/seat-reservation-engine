using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SeatReservation.Api.Tests.Support;
using SeatReservation.Application.Demo;
using SeatReservation.Domain.Enums;
using SeatReservation.Infrastructure.Persistence;

namespace SeatReservation.Api.Tests;

/// <summary>
/// Demo modunda misafirlerin onayladığı koltuklar süre sonra boşalır; tohum veri ve gerçek kullanıcılar korunur.
/// Saat sahte (FakeTimeProvider): 30 dakikayı gerçekten beklemeden sınanır.
/// </summary>
public sealed class DemoCleanupTests : IDisposable
{
    private readonly ApiFactory factory = new();

    public void Dispose() => factory.Dispose();

    private void ConfigureDemo(bool enabled, TimeSpan? lifetime = null)
    {
        var options = factory.Services.GetRequiredService<IOptions<DemoOptions>>().Value;
        options.Enabled = enabled;
        options.GuestSaleLifetime = lifetime ?? TimeSpan.FromMinutes(30);
    }

    /// <summary>Verilen e-postalı kullanıcı bir koltuğu tutup onaylar; koltuk ve rezervasyon kimliklerini döner.</summary>
    private async Task<(Guid SeatId, Guid ReservationId)> BuyAsync(string email)
    {
        var seatId = (await factory.CreateEventAsync()).Single();
        var (_, token) = await factory.CreateUserAsync(email: email);
        var client = factory.CreateClientWithToken(token);
        var hold = await client.PostAsync($"/api/seats/{seatId}/hold", null);
        var reservationId = (await hold.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var confirm = await client.PostAsync($"/api/reservations/{reservationId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        return (seatId, reservationId);
    }

    private static string GuestEmail() => DemoAccounts.NewGuestEmail();

    private async Task<int> CleanupAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DemoCleanupService>().ReleaseGuestSalesAsync(default);
    }

    private async Task<(SeatStatus Seat, ReservationStatus Reservation)> StatesAsync(Guid seatId, Guid reservationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return ((await db.Seats.FindAsync(seatId))!.Status, (await db.Reservations.FindAsync(reservationId))!.Status);
    }

    [Fact]
    public async Task Guest_sale_older_than_lifetime_is_released()
    {
        ConfigureDemo(enabled: true);
        var (seatId, reservationId) = await BuyAsync(GuestEmail());
        factory.Time.Advance(TimeSpan.FromMinutes(31));

        Assert.Equal(1, await CleanupAsync());

        Assert.Equal((SeatStatus.Available, ReservationStatus.Cancelled), await StatesAsync(seatId, reservationId));
    }

    [Fact]
    public async Task Guest_sale_younger_than_lifetime_is_kept()
    {
        ConfigureDemo(enabled: true);
        var (seatId, reservationId) = await BuyAsync(GuestEmail());
        factory.Time.Advance(TimeSpan.FromMinutes(29));

        Assert.Equal(0, await CleanupAsync());

        Assert.Equal((SeatStatus.Sold, ReservationStatus.Confirmed), await StatesAsync(seatId, reservationId));
    }

    [Fact]
    public async Task Seed_owner_is_kept_even_though_it_uses_the_demo_domain()
    {
        ConfigureDemo(enabled: true);
        var (seatId, reservationId) = await BuyAsync("tohum@demo.local");
        factory.Time.Advance(TimeSpan.FromHours(5));

        Assert.Equal(0, await CleanupAsync());

        Assert.Equal((SeatStatus.Sold, ReservationStatus.Confirmed), await StatesAsync(seatId, reservationId));
    }

    [Fact]
    public async Task Real_users_purchases_are_kept()
    {
        ConfigureDemo(enabled: true);
        var (seatId, reservationId) = await BuyAsync($"{Guid.NewGuid():N}@gmail.com");
        factory.Time.Advance(TimeSpan.FromHours(5));

        Assert.Equal(0, await CleanupAsync());

        Assert.Equal((SeatStatus.Sold, ReservationStatus.Confirmed), await StatesAsync(seatId, reservationId));
    }

    [Fact]
    public async Task Nothing_happens_when_demo_mode_is_off()
    {
        ConfigureDemo(enabled: false);
        var (seatId, reservationId) = await BuyAsync(GuestEmail());
        factory.Time.Advance(TimeSpan.FromHours(5));

        Assert.Equal(0, await CleanupAsync());

        Assert.Equal((SeatStatus.Sold, ReservationStatus.Confirmed), await StatesAsync(seatId, reservationId));
    }

    [Fact]
    public async Task Released_seat_can_be_held_by_the_next_visitor()
    {
        ConfigureDemo(enabled: true);
        var (seatId, _) = await BuyAsync(GuestEmail());
        // Ziyaretçi saat ilerlemeden oluşturulur: JWT doğrulaması gerçek saati kullanır, sahte saatle ileri tarihli token reddedilirdi.
        var (_, token) = await factory.CreateUserAsync();
        factory.Time.Advance(TimeSpan.FromMinutes(31));
        await CleanupAsync();

        var hold = await factory.CreateClientWithToken(token).PostAsync($"/api/seats/{seatId}/hold", null);

        Assert.Equal(HttpStatusCode.Created, hold.StatusCode);
    }

    [Fact]
    public async Task Public_registration_cannot_use_the_reserved_demo_domain()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email = "misafir-hile@demo.local", password = "correct-horse-battery" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
