using System.ComponentModel.DataAnnotations;

namespace SeatReservation.Api.Contracts;

public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(128)] string Password);

public sealed record LoginRequest(
    [Required, MaxLength(256)] string Email,
    [Required, MaxLength(128)] string Password);
