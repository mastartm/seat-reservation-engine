using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeatReservation.Api.Infrastructure;
using SeatReservation.Application.Reservations;

namespace SeatReservation.Api.Controllers;

[ApiController]
[Route("api/seats")]
[Authorize]
public class SeatsController(ReservationService reservations) : ControllerBase
{
    /// <summary>Koltuğu 10 dakikalığına tutar. Başkası tutuyor/satın almışsa 409.</summary>
    [HttpPost("{seatId:guid}/hold")]
    [ProducesResponseType<ReservationView>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Hold(Guid seatId, CancellationToken ct)
    {
        var reservation = await reservations.HoldSeatAsync(User.GetUserId(), seatId, ct);
        return StatusCode(StatusCodes.Status201Created, reservation);
    }
}
