using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeatReservation.Api.Contracts;
using SeatReservation.Application.Events;

namespace SeatReservation.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController(EventService events) : ControllerBase
{
    /// <summary>Etkinlik ve koltuk ızgarasını oluşturur. Yalnızca Admin.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<EventSummary>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateEventRequest request, CancellationToken ct)
    {
        var created = await events.CreateAsync(request.Name, request.StartsAt, request.Rows, request.SeatsPerRow, ct);
        return CreatedAtAction(nameof(GetSeats), new { eventId = created.Id }, created);
    }

    [HttpGet]
    public async Task<IReadOnlyList<EventSummary>> List(CancellationToken ct) => await events.ListAsync(ct);

    /// <summary>Koltuk haritası; süresi dolmuş tutmalar "Available" görünür.</summary>
    [HttpGet("{eventId:guid}/seats")]
    public async Task<IReadOnlyList<SeatView>> GetSeats(Guid eventId, CancellationToken ct) =>
        await events.GetSeatsAsync(eventId, ct);
}
