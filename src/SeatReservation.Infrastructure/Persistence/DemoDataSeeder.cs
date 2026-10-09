using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SeatReservation.Application.Abstractions;
using SeatReservation.Domain.Entities;
using SeatReservation.Domain.Enums;

namespace SeatReservation.Infrastructure.Persistence;

/// <summary>
/// Demo için örnek etkinlikler. Koltukların bir kısmı "satılmış" gelir ki ziyaretçi haritayı canlı görsün.
/// Yalnızca hiç etkinlik yokken çalışır: yeniden başlatmada çoğalmaz, Admin'in oluşturduğu veriye dokunmaz.
/// </summary>
public static class DemoDataSeeder
{
    // Sabit tohum: her ortamda aynı harita (demo ve ekran görüntüleri tekrarlanabilir olsun).
    private const int RandomSeed = 42;
    private const double SoldRatio = 0.25;

    public static async Task SeedAsync(IServiceProvider sp, CancellationToken ct = default)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        if (await db.Events.AnyAsync(ct)) return;

        var time = sp.GetRequiredService<TimeProvider>();
        var now = time.GetUtcNow();

        // Satılmış koltukların sahibi: kimsenin parolasını bilmediği bir hesap (giriş yapılamaz).
        // Demo ziyaretçisinin "Rezervasyonlarım"ında bu koltuklar görünmez; haritada yalnızca "satıldı" olarak çıkar.
        var seedUser = User.Create("tohum@demo.local", sp.GetRequiredService<IPasswordHasher>().DummyHash, UserRole.User, now);
        db.Users.Add(seedUser);

        var random = new Random(RandomSeed);
        foreach (var ev in new[]
                 {
                     Event.Create("Gece Konseri — Ana Salon", now.AddDays(14), rows: 8, seatsPerRow: 12),
                     Event.Create("Tiyatro: Hamlet", now.AddDays(30), rows: 6, seatsPerRow: 10),
                 })
        {
            db.Events.Add(ev);
            foreach (var seat in ev.Seats)
            {
                if (random.NextDouble() >= SoldRatio) continue;
                // Gerçek domain yolundan geçilir (Hold → Confirm): tohum veri de durum makinesinin kurallarına uyar.
                var reservation = seat.Hold(seedUser.Id, now, TimeSpan.FromMinutes(10));
                reservation.Confirm(seedUser.Id, now);
                db.Reservations.Add(reservation);
            }
        }

        await db.SaveChangesAsync(ct);
        sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DemoDataSeeder)).LogInformation("Demo verisi oluşturuldu.");
    }
}
