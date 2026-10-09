using SeatReservation.Domain.Common;
using SeatReservation.Domain.Enums;

namespace SeatReservation.Domain.Entities;

public sealed class User
{
    private User() { }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static User Create(string email, string passwordHash, UserRole role, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainValidationException("Geçerli bir e-posta gerekli.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainValidationException("Parola özeti boş olamaz.");

        return new User
        {
            Id = Guid.NewGuid(),
            // Küçük harfe normalize: "A@x.com" ile "a@x.com" aynı hesap sayılsın, tekillik indeksi buna dayanır.
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = role,
            CreatedAt = now,
        };
    }
}
