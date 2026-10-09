using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SeatReservation.Application.Abstractions;
using SeatReservation.Domain.Entities;
using SeatReservation.Domain.Enums;

namespace SeatReservation.Infrastructure.Persistence;

/// <summary>Açılışta (isteğe bağlı) migration uygular ve (isteğe bağlı) ilk Admin kullanıcıyı oluşturur.</summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));
        var db = sp.GetRequiredService<AppDbContext>();

        // Docker'da API, SQL Server'dan önce hazır olabilir; bu yüzden varsayılan kapalı, compose'ta açık.
        if (config.GetValue<bool>("Database:MigrateOnStartup"))
        {
            logger.LogInformation("Migration'lar uygulanıyor...");
            await db.Database.MigrateAsync(ct);
        }

        // Admin e-posta/parolası commit'lenmez; yalnızca ortam değişkeninden (Admin__Email, Admin__Password) gelir.
        var email = config["Admin:Email"];
        var password = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var users = sp.GetRequiredService<IUserRepository>();
        var normalized = email.Trim().ToLowerInvariant();
        if (await users.FindByEmailAsync(normalized, ct) is not null) return;

        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var time = sp.GetRequiredService<TimeProvider>();
        await users.AddAsync(User.Create(email, hasher.Hash(password), UserRole.Admin, time.GetUtcNow()), ct);
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        logger.LogInformation("Admin kullanıcı oluşturuldu: {Email}", normalized);
    }
}
