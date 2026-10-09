using Microsoft.Extensions.Options;
using SeatReservation.Application.Common;
using SeatReservation.Application.Reservations;

namespace SeatReservation.Api.BackgroundServices;

/// <summary>
/// Periyodik olarak süresi dolmuş tutmaları temizler. Temizlik bir optimizasyon değil düzeltme de değil,
/// veri hijyenidir: doğruluk zaten domain'deki süre kontrolleriyle sağlanır. Bu yüzden bir tur başarısız
/// olursa servis ölmez, loglayıp bir sonraki turu bekler.
/// </summary>
public sealed class HoldExpirationWorker(
    IServiceScopeFactory scopes,
    IOptions<ReservationOptions> options,
    ILogger<HoldExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.ExpirySweepInterval);
        do
        {
            await SweepAsync(stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            int released;
            do
            {
                // Her tur yeni scope: DbContext uzun ömürlü olmasın, önceki turdan kalan izlenen kayıtlar taşınmasın.
                await using var scope = scopes.CreateAsyncScope();
                released = await scope.ServiceProvider.GetRequiredService<HoldExpirationService>().ReleaseExpiredAsync(ct);
                if (released > 0) logger.LogInformation("{Count} süresi dolmuş tutma serbest bırakıldı.", released);
            }
            // Parti dolduysa geride kalan olabilir; hemen devam et.
            while (released == HoldExpirationService.BatchSize && !ct.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // kapanış
        }
        catch (ConcurrencyConflictException)
        {
            logger.LogInformation("Tarama sırasında koltuk başkası tarafından değiştirildi; sonraki turda yeniden denenecek.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Süre dolum taraması başarısız; sonraki turda yeniden denenecek.");
        }
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
