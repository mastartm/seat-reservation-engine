using SeatReservation.Application.Abstractions;

namespace SeatReservation.Application.Reservations;

/// <summary>Süresi dolmuş tutmaları kapatıp koltukları serbest bırakır.</summary>
public sealed class HoldExpirationService(
    IReservationRepository reservations,
    IUnitOfWork uow,
    TimeProvider time)
{
    public const int BatchSize = 200;

    /// <returns>Serbest bırakılan tutma sayısı; <see cref="BatchSize"/> ise geride kalan olabilir.</returns>
    /// <exception cref="Common.ConcurrencyConflictException">
    /// Tarama sırasında biri aynı koltuğu onayladı/devraldı. Sonraki tur güncel veriyi okuyup devam eder.
    /// </exception>
    public async Task<int> ReleaseExpiredAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var expired = await reservations.ListExpiredHoldsAsync(now, BatchSize, ct);

        var released = 0;
        foreach (var reservation in expired)
        {
            if (reservation.Expire(now)) released++;
        }

        if (released > 0) await uow.SaveChangesAsync(ct);
        return released;
    }
}
