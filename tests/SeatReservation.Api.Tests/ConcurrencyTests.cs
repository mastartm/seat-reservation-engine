using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeatReservation.Api.Tests.Support;
using SeatReservation.Domain.Enums;
using SeatReservation.Infrastructure.Persistence;

namespace SeatReservation.Api.Tests;

/// <summary>
/// Projenin temel sorusu: iki kişi aynı koltuğu aynı anda almaya çalışırsa ne olur?
/// Bu testler mock değil, gerçek HTTP hattı + gerçek EF Core + gerçek veritabanı (SQLite dosyası) üzerinde,
/// bir başlangıç kapısıyla (gate) aynı anda salınan gerçek paralel isteklerle cevap verir.
/// </summary>
public sealed class ConcurrencyTests : IDisposable
{
    private readonly ApiFactory factory = new();

    public void Dispose() => factory.Dispose();

    /// <summary>Tüm görevleri önce hazırlar, sonra kapıyı açıp aynı anda başlatır.</summary>
    private static async Task<T[]> RunSimultaneouslyAsync<T>(IEnumerable<Func<Task<T>>> work)
    {
        var gate = new TaskCompletionSource();
        var tasks = work.Select(w => Task.Run(async () =>
        {
            await gate.Task;
            return await w();
        })).ToArray();
        await Task.Delay(100); // görevlerin kapıda birikmesine izin ver
        gate.SetResult();
        return await Task.WhenAll(tasks);
    }

    private async Task<List<HttpClient>> CreateClientsAsync(int count)
    {
        var clients = new List<HttpClient>();
        for (var i = 0; i < count; i++)
            clients.Add(factory.CreateClientWithToken((await factory.CreateUserAsync()).Token));
        return clients;
    }

    [Fact]
    public async Task One_seat_100_parallel_holds_exactly_one_wins()
    {
        const int contenders = 100;
        var clients = await CreateClientsAsync(contenders);

        // Tek atışlık şans eseri geçişi elemek için 5 tur; her turda yeni koltuk, aynı 100 kullanıcı.
        for (var round = 0; round < 5; round++)
        {
            var seatId = (await factory.CreateEventAsync()).Single();

            var statuses = await RunSimultaneouslyAsync(
                clients.Select<HttpClient, Func<Task<HttpStatusCode>>>(c =>
                    async () => (await c.PostAsync($"/api/seats/{seatId}/hold", null)).StatusCode));

            Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.Created));
            Assert.Equal(contenders - 1, statuses.Count(s => s == HttpStatusCode.Conflict));

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(SeatStatus.Held, (await db.Seats.SingleAsync(s => s.Id == seatId)).Status);
            // Kaybedenlerin Reservation INSERT'ü seat UPDATE'iyle aynı transaction'daydı: geri alındı, veritabanında iz yok.
            Assert.Equal(1, await db.Reservations.CountAsync(r => r.SeatId == seatId));
        }
    }

    [Fact]
    public async Task Many_seats_contended_each_seat_gets_exactly_one_winner()
    {
        const int seatCount = 10, usersPerSeat = 10;
        var seatIds = await factory.CreateEventAsync(rows: 1, seatsPerRow: seatCount);
        var clients = await CreateClientsAsync(seatCount * usersPerSeat);

        var results = await RunSimultaneouslyAsync(
            clients.Select<HttpClient, Func<Task<(Guid Seat, HttpStatusCode Status)>>>((c, i) =>
            {
                var seat = seatIds[i % seatCount];
                return async () => (seat, (await c.PostAsync($"/api/seats/{seat}/hold", null)).StatusCode);
            }));

        Assert.All(seatIds, seat =>
        {
            var forSeat = results.Where(r => r.Seat == seat).Select(r => r.Status).ToList();
            Assert.Equal(1, forSeat.Count(s => s == HttpStatusCode.Created));
            Assert.Equal(usersPerSeat - 1, forSeat.Count(s => s == HttpStatusCode.Conflict));
        });
    }

    [Fact]
    public async Task Same_user_double_clicking_hold_creates_only_one_reservation()
    {
        var seatId = (await factory.CreateEventAsync()).Single();
        var (_, token) = await factory.CreateUserAsync();
        var client = factory.CreateClientWithToken(token);

        var statuses = await RunSimultaneouslyAsync(
            Enumerable.Range(0, 20).Select<int, Func<Task<HttpStatusCode>>>(_ =>
                async () => (await client.PostAsync($"/api/seats/{seatId}/hold", null)).StatusCode));

        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(19, statuses.Count(s => s == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Parallel_confirms_of_same_reservation_succeed_once()
    {
        var seatId = (await factory.CreateEventAsync()).Single();
        var (_, token) = await factory.CreateUserAsync();
        var client = factory.CreateClientWithToken(token);
        var hold = await client.PostAsync($"/api/seats/{seatId}/hold", null);
        var reservationId = (await hold.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var statuses = await RunSimultaneouslyAsync(
            Enumerable.Range(0, 20).Select<int, Func<Task<HttpStatusCode>>>(_ =>
                async () => (await client.PostAsync($"/api/reservations/{reservationId}/confirm", null)).StatusCode));

        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(19, statuses.Count(s => s == HttpStatusCode.Conflict));
    }

    private static async Task<Guid> HoldReservationAsync(HttpClient client, Guid seatId)
    {
        var hold = await client.PostAsync($"/api/seats/{seatId}/hold", null);
        return (await hold.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Parallel_cancels_of_same_reservation_succeed_once_and_free_the_seat()
    {
        var seatId = (await factory.CreateEventAsync()).Single();
        var (_, token) = await factory.CreateUserAsync();
        var client = factory.CreateClientWithToken(token);
        var reservationId = await HoldReservationAsync(client, seatId);

        var statuses = await RunSimultaneouslyAsync(
            Enumerable.Range(0, 20).Select<int, Func<Task<HttpStatusCode>>>(_ =>
                async () => (await client.PostAsync($"/api/reservations/{reservationId}/cancel", null)).StatusCode));

        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(19, statuses.Count(s => s == HttpStatusCode.Conflict));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(SeatStatus.Available, (await db.Seats.SingleAsync(s => s.Id == seatId)).Status);
    }

    [Fact]
    public async Task Cancel_racing_with_confirm_exactly_one_wins_and_state_stays_consistent()
    {
        // Aynı rezervasyonda iptal ↔ onay yarışı. Tek atışlık şansı elemek için 15 koltuk/tur.
        const int rounds = 15;
        var seatIds = await factory.CreateEventAsync(rows: 1, seatsPerRow: rounds);
        var (_, token) = await factory.CreateUserAsync();
        var client = factory.CreateClientWithToken(token);
        var reservationIds = new List<Guid>();
        foreach (var seatId in seatIds) reservationIds.Add(await HoldReservationAsync(client, seatId));

        var results = await RunSimultaneouslyAsync(reservationIds.SelectMany(id => new[]
        {
            Call(id, "cancel"),
            Call(id, "confirm"),
        }));

        Func<Task<(Guid Id, string Action, HttpStatusCode Status)>> Call(Guid id, string action) =>
            async () => (id, action, (await client.PostAsync($"/api/reservations/{id}/{action}", null)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var (id, seatId) in reservationIds.Zip(seatIds))
        {
            var pair = results.Where(r => r.Id == id).ToList();
            Assert.Equal(1, pair.Count(r => r.Status == HttpStatusCode.OK));
            Assert.Equal(1, pair.Count(r => r.Status == HttpStatusCode.Conflict));

            // Kazanan neyse veritabanındaki iki satır da ona uyar: yarım/çelişkili durum (ör. Confirmed + Available) yok.
            var winner = pair.Single(r => r.Status == HttpStatusCode.OK).Action;
            var reservation = await db.Reservations.SingleAsync(r => r.Id == id);
            var seat = await db.Seats.SingleAsync(s => s.Id == seatId);
            if (winner == "confirm")
                Assert.Equal((ReservationStatus.Confirmed, SeatStatus.Sold), (reservation.Status, seat.Status));
            else
                Assert.Equal((ReservationStatus.Cancelled, SeatStatus.Available), (reservation.Status, seat.Status));
        }
    }
}
