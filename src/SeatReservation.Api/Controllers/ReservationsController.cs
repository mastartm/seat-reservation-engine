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

    /// <summary>Oturumdaki kullanıcının rezervasyonları (yeniden eskiye).</summary>
    [HttpGet("mine")]
    public async Task<IReadOnlyList<ReservationView>> Mine(CancellationToken ct) =>
        await reservations.ListMineAsync(User.GetUserId(), ct);
}
