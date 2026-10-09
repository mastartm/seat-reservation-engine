using SeatReservation.Application.Abstractions;
using SeatReservation.Application.Common;
using SeatReservation.Domain.Entities;
using SeatReservation.Domain.Enums;

namespace SeatReservation.Application.Events;

public sealed record EventSummary(Guid Id, string Name, DateTimeOffset StartsAt);

public sealed record SeatView(Guid Id, string Label, SeatStatus Status);

public sealed class EventService(IEventRepository events, IUnitOfWork uow, TimeProvider time)
{
    public async Task<EventSummary> CreateAsync(string name, DateTimeOffset startsAt, int rows, int seatsPerRow, CancellationToken ct)
    {
        var ev = Event.Create(name, startsAt, rows, seatsPerRow);
        await events.AddAsync(ev, ct);
        await uow.SaveChangesAsync(ct);
        return new EventSummary(ev.Id, ev.Name, ev.StartsAt);
    }

    public async Task<IReadOnlyList<EventSummary>> ListAsync(CancellationToken ct) =>
        (await events.ListAsync(ct)).OrderBy(e => e.StartsAt).Select(e => new EventSummary(e.Id, e.Name, e.StartsAt)).ToList();

    public async Task<IReadOnlyList<SeatView>> GetSeatsAsync(Guid eventId, CancellationToken ct)
    {
        var ev = await events.GetWithSeatsAsync(eventId, ct) ?? throw new NotFoundException("Etkinlik bulunamadı.");
        var now = time.GetUtcNow();

        // StatusAt: süresi dolmuş ama henüz temizlenmemiş tutmalar kullanıcıya "boş" görünür.
        return ev.Seats
            .OrderBy(s => s.Label.Length > 0 ? s.Label[0] : ' ')
            .ThenBy(s => int.Parse(s.Label[1..]))
            .Select(s => new SeatView(s.Id, s.Label, s.StatusAt(now)))
            .ToList();
    }
}
