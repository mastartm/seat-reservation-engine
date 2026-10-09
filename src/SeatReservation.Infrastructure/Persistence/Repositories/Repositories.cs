using Microsoft.EntityFrameworkCore;
using SeatReservation.Application.Abstractions;
using SeatReservation.Application.Common;
using SeatReservation.Domain.Entities;
using SeatReservation.Domain.Enums;

namespace SeatReservation.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

    public Task<User?> GetAsync(Guid id, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task AddAsync(User user, CancellationToken ct) => await db.Users.AddAsync(user, ct);
}

internal sealed class EventRepository(AppDbContext db) : IEventRepository
{
    public async Task AddAsync(Event ev, CancellationToken ct) => await db.Events.AddAsync(ev, ct);

    public async Task<IReadOnlyList<Event>> ListAsync(CancellationToken ct) =>
        await db.Events.AsNoTracking().ToListAsync(ct);

    public Task<Event?> GetWithSeatsAsync(Guid eventId, CancellationToken ct) =>
        db.Events.AsNoTracking().Include(e => e.Seats).FirstOrDefaultAsync(e => e.Id == eventId, ct);
}

internal sealed class SeatRepository(AppDbContext db) : ISeatRepository
{
    public Task<Seat?> GetAsync(Guid seatId, CancellationToken ct) =>
        db.Seats.FirstOrDefaultAsync(s => s.Id == seatId, ct);
}

internal sealed class ReservationRepository(AppDbContext db) : IReservationRepository
{
    public async Task AddAsync(Reservation reservation, CancellationToken ct) =>
        await db.Reservations.AddAsync(reservation, ct);

    public Task<Reservation?> GetWithSeatAsync(Guid reservationId, CancellationToken ct) =>
        db.Reservations.Include(r => r.Seat).FirstOrDefaultAsync(r => r.Id == reservationId, ct);

    public async Task<IReadOnlyList<Reservation>> ListByUserAsync(Guid userId, CancellationToken ct) =>
        await db.Reservations.AsNoTracking().Include(r => r.Seat)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Reservation>> ListExpiredHoldsAsync(DateTimeOffset now, int take, CancellationToken ct) =>
        await db.Reservations.Include(r => r.Seat)
            .Where(r => r.Status == ReservationStatus.Held && r.ExpiresAt <= now)
            .OrderBy(r => r.ExpiresAt)
            .Take(take)
            .ToListAsync(ct);
}

internal sealed class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            // SaveChanges tek bir veritabanı transaction'ı açar: aynı çağrıdaki Reservation INSERT'ü ile
            // Seat UPDATE'i ya birlikte yazılır ya birlikte geri alınır. Seat UPDATE'i RowVersion
            // uyuşmadığı için 0 satır etkilerse tüm transaction geri alınır.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("Kayıt başka bir istek tarafından değiştirildi.", ex);
        }
        // DbUpdateConcurrencyException da DbUpdateException'dan türer; sıra bilerek böyle (önce özel olan).
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new UniqueConstraintViolationException("Benzersiz olması gereken bir değer zaten kayıtlı.", ex);
        }
    }

    /// <summary>
    /// SQL Server: hata numarası 2601 (benzersiz index) / 2627 (UNIQUE kısıtı). Numara kullanılır, mesaj metni değil:
    /// SQL Server mesajları sunucu diline göre değişir. SQLite (yalnızca testlerde) sürücüsü bu projede referanslı
    /// olmadığı için tip adı ve "UNIQUE" metniyle tanınır.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException switch
    {
        Microsoft.Data.SqlClient.SqlException sql => sql.Number is 2601 or 2627,
        { } inner => inner.GetType().Name == "SqliteException" && inner.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };
}
