using SeatReservation.Application.Abstractions;
using SeatReservation.Application.Common;
using SeatReservation.Domain.Entities;
using SeatReservation.Domain.Enums;

namespace SeatReservation.Application.Auth;

public sealed record AuthResult(string Token, DateTimeOffset ExpiresAt, Guid UserId, string Email, UserRole Role);

public sealed class AuthService(
    IUserRepository users,
    IUnitOfWork uow,
    IPasswordHasher hasher,
    ITokenService tokens,
    TimeProvider time)
{
    public const int MinPasswordLength = 8;

    /// <summary>Herkese açık kayıt her zaman <see cref="UserRole.User"/> üretir; Admin yalnızca sunucu tarafında tohumlanır.</summary>
    public async Task<AuthResult> RegisterAsync(string email, string password, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            throw new RequestValidationException($"Parola en az {MinPasswordLength} karakter olmalı.");

        var user = User.Create(email, hasher.Hash(password), UserRole.User, time.GetUtcNow());
        if (await users.FindByEmailAsync(user.Email, ct) is not null)
            throw new EmailAlreadyRegisteredException("Bu e-posta zaten kayıtlı.");

        await users.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);
        return ToResult(user);
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync((email ?? string.Empty).Trim().ToLowerInvariant(), ct);

        // Kullanıcı yoksa da hash doğrulaması yapılır ki yanıt süresi "e-posta kayıtlı mı?" bilgisini sızdırmasın.
        var ok = hasher.Verify(password ?? string.Empty, user?.PasswordHash ?? hasher.DummyHash);
        if (user is null || !ok) throw new InvalidCredentialsException();

        return ToResult(user);
    }

    private AuthResult ToResult(User user)
    {
        var token = tokens.Create(user);
        return new AuthResult(token.Value, token.ExpiresAt, user.Id, user.Email, user.Role);
    }
}
