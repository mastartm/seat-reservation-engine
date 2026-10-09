using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeatReservation.Api.Infrastructure;
using SeatReservation.Application.Reservations;

namespace SeatReservation.Api.Controllers;

[ApiController]
[Route("api/reservations")]
[Authorize]
public class ReservationsController(ReservationService reservations) : ControllerBase
{
    /// <summary>Satın almayı onaylar. Yalnızca tutmanın sahibi, süre dolmadan.</summary>
    [HttpPost("{reservationId:guid}/confirm")]
    public async Task<ReservationView> Confirm(Guid reservationId, CancellationToken ct) =>
        await reservations.ConfirmAsync(User.GetUserId(), reservationId, ct);

    /// <summary>
    /// Tutmadan vazgeçer; koltuk hemen boşalır. Yalnızca tutma sahibi (diğerleri 403); onaylanmış (satılmış) 409.
    /// </summary>
    [HttpPost("{reservationId:guid}/cancel")]
    public async Task<ReservationView> Cancel(Guid reservationId, CancellationToken ct) =>
        await reservations.CancelAsync(User.GetUserId(), reservationId, ct);

    /// <summary>Oturumdaki kullanıcının rezervasyonları (yeniden eskiye).</summary>
    [HttpGet("mine")]
    public async Task<IReadOnlyList<ReservationView>> Mine(CancellationToken ct) =>
        await reservations.ListMineAsync(User.GetUserId(), ct);
}
