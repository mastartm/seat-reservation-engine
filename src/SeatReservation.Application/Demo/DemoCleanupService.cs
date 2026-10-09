using Microsoft.Extensions.Options;
using SeatReservation.Application.Abstractions;

namespace SeatReservation.Application.Demo;

/// <summary>
/// Demo modunda misafirlerin onayladığı koltukları belirli süre sonra serbest bırakır. Aksi hâlde her ziyaretçi birkaç
/// koltuk alıp çıkınca harita zamanla tamamen "Satıldı" olur ve demo kullanılamaz hâle gelir.
/// Tohum veri (<c>tohum@demo.local</c>) ve gerçek kullanıcıların satın almaları korunur; demo kapalıyken hiçbir şey yapmaz.
/// </summary>
public sealed class DemoCleanupService(
    IReservationRepository reservations,
    IUnitOfWork uow,
    IOptions<DemoOptions> demo,
    TimeProvider time)
{
    public const int BatchSize = 200;

    /// <returns>Serbest bırakılan satın alma sayısı; <see cref="BatchSize"/> ise geride kalan olabilir.</returns>
    /// <exception cref="Common.ConcurrencyConflictException">Tarama sırasında koltuk değişti; sonraki tur devam eder.</exception>
    public async Task<int> ReleaseGuestSalesAsync(CancellationToken ct)
    {
        var options = demo.Value;
        if (!options.Enabled || options.GuestSaleLifetime <= TimeSpan.Zero) return 0;

        var cutoff = time.GetUtcNow() - options.GuestSaleLifetime;
        var sales = await reservations.ListConfirmedGuestSalesAsync(cutoff, BatchSize, ct);

        var released = 0;
        foreach (var sale in sales)
        {
            if (sale.RevokeSale()) released++;
        }

        if (released > 0) await uow.SaveChangesAsync(ct);
        return released;
    }
}
