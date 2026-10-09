using System.ComponentModel.DataAnnotations;

namespace SeatReservation.Api.Contracts;

public sealed record CreateEventRequest(
    [Required, MaxLength(200)] string Name,
    DateTimeOffset StartsAt,
    [Range(1, 26)] int Rows,
    [Range(1, 100)] int SeatsPerRow);
