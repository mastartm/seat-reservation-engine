using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SeatReservation.Infrastructure.Persistence;

/// <summary>
/// Yalnızca <c>dotnet ef migrations ...</c> için. Migration üretmek veritabanına bağlanmayı gerektirmez,
/// bu yüzden bağlantı dizesi ortam değişkeninden gelir; yoksa sahte bir yer tutucu kullanılır.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                 ?? "Server=localhost;Database=SeatReservation;Integrated Security=true;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cs).Options;
        return new AppDbContext(options);
    }
}
