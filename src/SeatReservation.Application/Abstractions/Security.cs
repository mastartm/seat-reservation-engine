using SeatReservation.Domain.Entities;

namespace SeatReservation.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);

    /// <summary>Var olmayan kullanıcı için de aynı maliyette doğrulama yapabilmek adına geçerli biçimde sahte özet.</summary>
    string DummyHash { get; }
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken Create(User user);
}
