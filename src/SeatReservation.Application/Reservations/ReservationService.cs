using Microsoft.Extensions.Options;
using SeatReservation.Application.Abstractions;
using SeatReservation.Application.Common;
using SeatReservation.Domain.Entities;
using SeatReservation.Domain.Enums;

namespace SeatReservation.Application.Reservations;

public sealed record ReservationView(
    Guid Id,
    Guid SeatId,
    Guid EventId,
    string SeatLabel,
    ReservationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ConfirmedAt)
{
    internal static ReservationView From(Reservation r) =>
        new(r.Id, r.SeatId, r.Seat.EventId, r.Seat.Label, r.Status, r.CreatedAt, r.ExpiresAt, r.ConfirmedAt);
}

public sealed class ReservationService(
    ISeatRepository seats,
    IReservationRepository reservations,
    IUnitOfWork uow,
    IOptions<ReservationOptions> options,
    TimeProvider time)
{
    /// <summary>
    /// Koltuğu tutar. Aynı koltuğa aynı anda gelen N isteğin tam biri başarılı olur:
    /// <list type="bullet">
    /// <item>İstek, koltuğu okuduğunda zaten tutuluysa → domain <c>SeatNotAvailableException</c> (409).</item>
    /// <item>İstekler koltuğu hâlâ boşken okumuşsa → hepsi Hold() der, ama UPDATE'te yalnızca ilki
    /// RowVersion'ı tutturur; diğerleri <c>ConcurrencyConflictException</c> alır (409).</item>
    /// </list>
    /// Her iki yol da aynı sonucu verir; yeniden deneme yapılmaz, çünkü kaybeden için cevap zaten "koltuk alındı".
    /// </summary>
    public async Task<ReservationView> HoldSeatAsync(Guid userId, Guid seatId, CancellationToken ct)
    {
        var seat = await seats.GetAsync(seatId, ct) ?? throw new NotFoundException("Koltuk bulunamadı.");

        var now = time.GetUtcNow();
        var max = options.Value.MaxActiveReservationsPerUser;
        if (max > 0 && await reservations.CountActiveByUserAsync(userId, now, ct) >= max)
            throw new ReservationLimitExceededException(
                $"Aynı anda en fazla {max} koltuğun olabilir (tutma + satın alma). Önce birinden vazgeç.");

        var reservation = seat.Hold(userId, now, options.Value.HoldDuration);
        await reservations.AddAsync(reservation, ct);
        await uow.SaveChangesAsync(ct);

        return ReservationView.From(reservation);
    }

    /// <summary>
    /// Satın almayı onaylar. Kural kontrolleri (sahiplik, süre, durum) domain'de; burada sadece yükle-uygula-kaydet.
    /// Onay ile süre dolum servisi aynı anda aynı koltuğa dokunursa RowVersion biri geçirir, diğeri 409 alır:
    /// onaylanmış koltuk yanlışlıkla serbest kalamaz.
    /// </summary>
    public async Task<ReservationView> ConfirmAsync(Guid userId, Guid reservationId, CancellationToken ct)
    {
        var reservation = await reservations.GetWithSeatAsync(reservationId, ct)
                          ?? throw new NotFoundException("Rezervasyon bulunamadı.");

        reservation.Confirm(userId, time.GetUtcNow());
        await uow.SaveChangesAsync(ct);

        return ReservationView.From(reservation);
    }

    /// <summary>
    /// Tutmadan vazgeçer. Kurallar (sahiplik, onaylanmışsa red, süre) domain'de. İptal ile süre dolum servisi (veya onay)
    /// aynı anda aynı koltuğu bırakmaya/değiştirmeye çalışırsa RowVersion biri geçirir, diğeri 409 alır.
    /// </summary>
    public async Task<ReservationView> CancelAsync(Guid userId, Guid reservationId, CancellationToken ct)
    {
        var reservation = await reservations.GetWithSeatAsync(reservationId, ct)
                          ?? throw new NotFoundException("Rezervasyon bulunamadı.");

        reservation.Cancel(userId, time.GetUtcNow());
        await uow.SaveChangesAsync(ct);

        return ReservationView.From(reservation);
    }

    public async Task<IReadOnlyList<ReservationView>> ListMineAsync(Guid userId, CancellationToken ct) =>
        (await reservations.ListByUserAsync(userId, ct)).Select(ReservationView.From).ToList();
}
