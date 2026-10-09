using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeatReservation.Api.BackgroundServices;
using SeatReservation.Api.Tests.Support;
using SeatReservation.Application.Abstractions;
using SeatReservation.Application.Common;
using SeatReservation.Application.Reservations;
using SeatReservation.Domain.Enums;
using SeatReservation.Infrastructure.Persistence;

namespace SeatReservation.Api.Tests;

public sealed class HoldExpirationTests : IDisposable
{
    private readonly ApiFactory factory = new();

    public void Dispose() => factory.Dispose();

    private async Task<(Guid ReservationId, Guid SeatId, Guid UserId, HttpClient Client)> HoldAsync()
    {
        var seatId = (await factory.CreateEventAsync()).Single();
        var (user, token) = await factory.CreateUserAsync();
        var client = factory.CreateClientWithToken(token);
        var hold = await client.PostAsync($"/api/seats/{seatId}/hold", null);
        return ((await hold.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(), seatId, user.Id, client);
    }

    private async Task<int> SweepAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<HoldExpirationService>().ReleaseExpiredAsync(default);
    }

    private async Task<(SeatStatus Seat, ReservationStatus Reservation)> StoredStatesAsync(Guid seatId, Guid reservationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return ((await db.Seats.FindAsync(seatId))!.Status, (await db.Reservations.FindAsync(reservationId))!.Status);
    }

    [Fact]
    public async Task Sweep_releases_expired_hold_in_the_database()
    {
        var (reservationId, seatId, _, _) = await HoldAsync();
        factory.Time.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(1, await SweepAsync());

        Assert.Equal((SeatStatus.Available, ReservationStatus.Expired), await StoredStatesAsync(seatId, reservationId));
    }

    [Fact]
    public async Task Sweep_leaves_live_holds_alone()
    {
        var (reservationId, seatId, _, _) = await HoldAsync();
        factory.Time.Advance(TimeSpan.FromMinutes(9));

        Assert.Equal(0, await SweepAsync());

        Assert.Equal((SeatStatus.Held, ReservationStatus.Held), await StoredStatesAsync(seatId, reservationId));
    }

    [Fact]
    public async Task Sweep_leaves_confirmed_reservations_alone()
    {
        var (reservationId, seatId, _, client) = await HoldAsync();
        await client.PostAsync($"/api/reservations/{reservationId}/confirm", null);
        factory.Time.Advance(TimeSpan.FromHours(1));

        Assert.Equal(0, await SweepAsync());

        Assert.Equal((SeatStatus.Sold, ReservationStatus.Confirmed), await StoredStatesAsync(seatId, reservationId));
    }

    [Fact]
    public async Task Sweep_does_not_release_a_seat_that_someone_else_took_over()
    {
        var (staleId, seatId, _, _) = await HoldAsync();
        factory.Time.Advance(TimeSpan.FromMinutes(10));
        // Devralma HTTP yerine doğrudan servisle: saat ilerletildiği için yeni token'ın 'nbf' değeri gerçek saatin ilerisinde kalırdı.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = Domain.Entities.User.Create("n@test.local", "x", UserRole.User, factory.Time.GetUtcNow());
            db.Users.Add(user);
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<ReservationService>().HoldSeatAsync(user.Id, seatId, default);
        }

        await SweepAsync();

        var (seat, reservation) = await StoredStatesAsync(seatId, staleId);
        Assert.Equal(SeatStatus.Held, seat); // yeni sahibin tutması bozulmadı
        Assert.Equal(ReservationStatus.Expired, reservation);
    }

    [Fact]
    public async Task Confirm_racing_with_sweep_cannot_both_win()
    {
        var (reservationId, seatId, userId, _) = await HoldAsync();

        // A: onay isteği süre dolmadan koltuğu okuyup onayı uyguladı ama henüz kaydetmedi.
        using var scopeA = factory.Services.CreateScope();
        var reservation = await scopeA.ServiceProvider.GetRequiredService<IReservationRepository>()
            .GetWithSeatAsync(reservationId, default);
        reservation!.Confirm(userId, factory.Time.GetUtcNow().AddMinutes(9));

        // B: süre dolduktan sonra tarama koltuğu serbest bırakıp kaydetti.
        factory.Time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(1, await SweepAsync());

        // A kaydetmeye çalışınca RowVersion uyuşmaz: onay kaybeder, koltuk serbest kalır, "satıldı" yazılmaz.
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            scopeA.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default));
        Assert.Equal((SeatStatus.Available, ReservationStatus.Expired), await StoredStatesAsync(seatId, reservationId));
    }

    [Fact]
    public async Task Worker_releases_expired_holds_on_its_own()
    {
        var (reservationId, seatId, _, _) = await HoldAsync();
        factory.Time.Advance(TimeSpan.FromMinutes(10));
        var worker = new HoldExpirationWorker(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ReservationOptions { ExpirySweepInterval = TimeSpan.FromMilliseconds(50) }),
            NullLogger<HoldExpirationWorker>.Instance);

        await worker.StartAsync(default);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline &&
                   (await StoredStatesAsync(seatId, reservationId)).Seat != SeatStatus.Available)
                await Task.Delay(50);
        }
        finally
        {
            await worker.StopAsync(default);
        }

        Assert.Equal((SeatStatus.Available, ReservationStatus.Expired), await StoredStatesAsync(seatId, reservationId));
    }
}
