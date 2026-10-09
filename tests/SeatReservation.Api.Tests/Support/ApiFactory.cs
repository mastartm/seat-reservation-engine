using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
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
/// Gerçek HTTP hattı (JWT, filtreler, exception handler dahil) + gerçek EF Core.
/// Varsayılan veritabanı SQLite dosyasıdır (Docker gerektirmez); dosya tabanlı çünkü bellek içi SQLite tek
/// bağlantıya bağlıdır ve paralel istekleri gerçekten sınayamazdı.
/// <c>TEST_SQLSERVER_CONNECTION</c> doluysa (CI'daki <c>sqlserver</c> işi) bunun yerine gerçek SQL Server kullanılır:
/// her factory kendi benzersiz adlı veritabanını açar, Dispose'ta siler.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string SqlServerEnvVar = "TEST_SQLSERVER_CONNECTION";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"seats-test-{Guid.NewGuid():N}.db");

    // Boşsa SQLite. Doluysa sunucu bağlantısı; veritabanı adı her factory için ayrı (testler birbirinin verisini görmez).
    private readonly SqlConnectionStringBuilder? _sqlServer = CreateSqlServerConnection();

    private static SqlConnectionStringBuilder? CreateSqlServerConnection()
    {
        var raw = Environment.GetEnvironmentVariable(SqlServerEnvVar);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return new SqlConnectionStringBuilder(raw) { InitialCatalog = $"seats_test_{Guid.NewGuid():N}" };
    }

    public FakeTimeProvider Time { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", "test-only-signing-key-0123456789-abcdef");

        // SQL Server yolunda DbContext'e dokunulmaz: üretimdeki AddInfrastructure kaydı (UseSqlServer + gerçek
        // `rowversion`) bağlantı dizesi dışında aynen kullanılır; SqliteAppDbContext'in RowVersion yaması uygulanmaz.
        if (_sqlServer is not null) builder.UseSetting("ConnectionStrings:Default", _sqlServer.ConnectionString);

        builder.ConfigureServices(services =>
        {
            if (_sqlServer is null)
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext, SqliteAppDbContext>(o =>
                    o.UseSqlite($"Data Source={_dbPath};Default Timeout=30"));
            }

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
        if (_sqlServer is not null)
        {
            // EnsureCreated değil Migrate: şemayı modelden değil migration'lardan kurar, yani üretimde (compose,
            // Database__MigrateOnStartup) çalışan yolun aynısı sınanır. Migration ile model uyumsuzsa (ör. rowversion
            // kolonu eksik) eş zamanlılık testleri sessizce yanlış şemada koşmak yerine burada kırılır.
            db.Database.Migrate();
            return host;
        }

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
        if (_sqlServer is not null) DropSqlServerDatabase();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            try { File.Delete(_dbPath + suffix); } catch (IOException) { /* sonraki temp temizliği alır */ }
        }
    }

    private void DropSqlServerDatabase()
    {
        var name = _sqlServer!.InitialCatalog;
        try
        {
            // Havuzdaki açık bağlantılar DROP'u engeller; önce bırakılır, sonra SINGLE_USER ile kalanlar atılır.
            SqlConnection.ClearAllPools();
            using var connection = new SqlConnection(new SqlConnectionStringBuilder(_sqlServer.ConnectionString)
                { InitialCatalog = "master", Pooling = false }.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            // Veritabanı adı parametre olarak verilemez (tanımlayıcı); bu yüzden ad parametreyle gider, sunucu tarafında
            // QUOTENAME ile kaçışlanıp birleştirilir. Ad zaten kendi ürettiğimiz bir GUID'den gelir.
            command.CommandText = """
                IF DB_ID(@name) IS NOT NULL
                BEGIN
                    DECLARE @sql nvarchar(max) =
                        N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; ' +
                        N'DROP DATABASE ' + QUOTENAME(@name) + N';';
                    EXEC (@sql);
                END
                """;
            command.Parameters.AddWithValue("@name", name);
            command.ExecuteNonQuery();
        }
        catch (SqlException)
        {
            // Temizlik başarısızsa testi kırma; CI sunucusu zaten işin sonunda atılır.
        }
    }
}
