using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SeatReservation.Domain.Entities;
using SeatReservation.Infrastructure.Persistence;

namespace SeatReservation.Api.Tests.Support;

/// <summary>
/// Testlerde SQL Server yerine SQLite (Docker gerektirmez). İki fark telafi edilir:
/// 1) SQLite rowversion üretmez → SaveChanges öncesi değeri biz yenileriz; EF yine
///    UPDATE ... WHERE RowVersion = @orijinal üretir, yani aynı optimistic concurrency mekanizması çalışır.
/// 2) SQLite, DateTimeOffset'i sıralayamaz/karşılaştıramaz → long'a çevrilir (UTC değerlerde sıra korunur).
/// </summary>
public sealed class SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options) : AppDbContext(options)
{
    protected override void ConfigureRowVersion(PropertyBuilder<byte[]> property) =>
        property.IsConcurrencyToken().ValueGeneratedNever();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<Seat>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            entry.Property(s => s.RowVersion).CurrentValue = Guid.NewGuid().ToByteArray();
        }
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
