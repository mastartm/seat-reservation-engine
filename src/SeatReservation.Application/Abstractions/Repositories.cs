using SeatReservation.Domain.Entities;

namespace SeatReservation.Application.Abstractions;

// Application EF Core'u bilmez; bu arayüzleri Infrastructure gerçekler.
// Metotlar yalnızca use-case'lerin ihtiyacı kadar: genel amaçlı IRepository<T> yok.

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken ct);
    Task<User?> GetAsync(Guid id, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
}

public interface IEventRepository
{
    Task AddAsync(Event ev, CancellationToken ct);
    Task<IReadOnlyList<Event>> ListAsync(CancellationToken ct);
    Task<Event?> GetWithSeatsAsync(Guid eventId, CancellationToken ct);
}

public interface ISeatRepository
{
    /// <summary>Koltuğu izleme (tracking) açık yükler; RowVersion özgün değeri buradan gelir.</summary>
    Task<Seat?> GetAsync(Guid seatId, CancellationToken ct);
}

public interface IReservationRepository
{
    Task AddAsync(Reservation reservation, CancellationToken ct);
    Task<Reservation?> GetWithSeatAsync(Guid reservationId, CancellationToken ct);
    Task<IReadOnlyList<Reservation>> ListByUserAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<Reservation>> ListExpiredHoldsAsync(DateTimeOffset now, int take, CancellationToken ct);
}

public interface IUnitOfWork
{
    /// <exception cref="Common.ConcurrencyConflictException">RowVersion uyuşmazlığı.</exception>
    Task SaveChangesAsync(CancellationToken ct);
}
