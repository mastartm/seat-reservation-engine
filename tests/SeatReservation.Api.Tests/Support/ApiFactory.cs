using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SeatReservation.Application.Abstractions;
using SeatReservation.Domain.Entities;
using SeatReservation.Domain.Enums;
using SeatReservation.Infrastructure.Persistence;

namespace SeatReservation.Api.Tests.Support;

/// <summary>
/// Gerçek HTTP hattı (JWT, filtreler, exception handler dahil) + gerçek EF Core, yalnızca veritabanı SQLite dosyası.
/// Dosya tabanlı: bellek içi SQLite tek bağlantıya bağlı olduğundan paralel istekleri gerçekten sınayamazdı.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"seats-test-{Guid.NewGuid():N}.db");

    public FakeTimeProvider Time { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", "test-only-signing-key-0123456789-abcdef");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext, SqliteAppDbContext>(o =>
                o.UseSqlite($"Data Source={_dbPath};Default Timeout=30"));

            // Arka plan servisi testlerde kapalı: gerçek zamanlayıcı sahte saatle yarışıp testleri belirsizleştirirdi.
            // Tarama mantığı HoldExpirationService üzerinden doğrudan, worker ise ayrı bir testte elle sınanır.
            services.RemoveAll<IHostedService>();

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        // WAL: yazan beklerken okuyanlar bloklanmaz; paralel testte gerçek bir veritabanına daha yakın davranış.
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        return host;
    }

    /// <summary>Doğrudan veritabanına kullanıcı ekler (parola hash'lemeden, hızlı) ve ona ait token döner.</summary>
    public async Task<(User User, string Token)> CreateUserAsync(UserRole role = UserRole.User)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = User.Create($"{Guid.NewGuid():N}@test.local", "not-a-real-hash", role, Time.GetUtcNow());
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user, scope.ServiceProvider.GetRequiredService<ITokenService>().Create(user).Value);
    }

    /// <summary>Etkinlik ve koltuklarını doğrudan veritabanına yazar; koltuk kimliklerini etiket sırasıyla döner.</summary>
    public async Task<IReadOnlyList<Guid>> CreateEventAsync(int rows = 1, int seatsPerRow = 1)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = Event.Create("Test etkinliği", Time.GetUtcNow().AddDays(30), rows, seatsPerRow);
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev.Seats.Select(s => s.Id).ToList();
    }

    public HttpClient CreateClientWithToken(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            try { File.Delete(_dbPath + suffix); } catch (IOException) { /* sonraki temp temizliği alır */ }
        }
    }
}
