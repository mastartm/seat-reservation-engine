using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeatReservation.Domain.Entities;

namespace SeatReservation.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Test projesi SQLite ile türetilmiş bir bağlam kullanabilsin diye (bkz. ConfigureRowVersion).
    protected AppDbContext(DbContextOptions options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(u => u.Id);
            b.Property(u => u.Email).HasMaxLength(256).IsRequired();
            b.HasIndex(u => u.Email).IsUnique();
            b.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            b.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Event>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Name).HasMaxLength(200).IsRequired();
            b.HasMany(e => e.Seats).WithOne().HasForeignKey(s => s.EventId).OnDelete(DeleteBehavior.Cascade);
            // Event.Seats salt-okunur; EF koleksiyonu private alan üzerinden doldursun.
            b.Navigation(e => e.Seats).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Seat>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.Label).HasMaxLength(10).IsRequired();
            b.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(s => new { s.EventId, s.Label }).IsUnique();
            ConfigureRowVersion(b.Property(s => s.RowVersion));
        });

        modelBuilder.Entity<Reservation>(b =>
        {
            b.HasKey(r => r.Id);
            b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            b.HasOne(r => r.Seat).WithMany().HasForeignKey(r => r.SeatId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r => r.UserId);
            // Süre dolum servisi "Status = Held AND ExpiresAt <= now" sorgusunu her turda çalıştırır.
            b.HasIndex(r => new { r.Status, r.ExpiresAt });
        });
    }

    /// <summary>
    /// SQL Server'da <c>rowversion</c>: her UPDATE'te veritabanı değeri kendisi artırır, EF de
    /// UPDATE ... WHERE Id = @id AND RowVersion = @orijinal üretir. Etkilenen satır 0 ise
    /// DbUpdateConcurrencyException fırlar: kayıt biz okuduktan sonra başkası değiştirmiştir.
    /// SQLite rowversion üretmez; testteki türetilmiş bağlam bu metodu geçersiz kılar.
    /// </summary>
    protected virtual void ConfigureRowVersion(PropertyBuilder<byte[]> property) => property.IsRowVersion();
}
