using SeatReservation.Application.Abstractions;
using Microsoft.Extensions.Options;
using SeatReservation.Application.Common;
using SeatReservation.Application.Demo;
using SeatReservation.Domain.Entities;
using SeatReservation.Domain.Enums;

namespace SeatReservation.Application.Auth;

public sealed record AuthResult(string Token, DateTimeOffset ExpiresAt, Guid UserId, string Email, UserRole Role);

public sealed class AuthService(
    IUserRepository users,
    IUnitOfWork uow,
    IPasswordHasher hasher,
    ITokenService tokens,
    IOptions<DemoOptions> demo,
    TimeProvider time)
{
    public const int MinPasswordLength = 8;

    /// <summary>Herkese açık kayıt her zaman <see cref="UserRole.User"/> üretir; Admin yalnızca sunucu tarafında tohumlanır.</summary>
    public async Task<AuthResult> RegisterAsync(string email, string password, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            throw new RequestValidationException($"Parola en az {MinPasswordLength} karakter olmalı.");

        var user = User.Create(email, hasher.Hash(password), UserRole.User, time.GetUtcNow());
        // Demo hesapları (misafir, tohum sahibi) bu alan adını kullanır; biri bu alan adıyla kayıt olup misafir gibi
        // davranmasın (demo temizliği misafirleri e-postasından tanır).
        if (DemoAccounts.IsReservedDomain(user.Email))
            throw new RequestValidationException($"{DemoAccounts.ReservedDomain} alan adı demo hesaplarına ayrılmıştır.");
        if (await users.FindByEmailAsync(user.Email, ct) is not null)
            throw new EmailAlreadyRegisteredException("Bu e-posta zaten kayıtlı.");

        await users.AddAsync(user, ct);
        try
        {
            await uow.SaveChangesAsync(ct);
        }
        // Yukarıdaki "var mı?" kontrolü ile kayıt arasında aynı e-postayla gelen ikinci istek geçebilir (yarış).
        // Son söz veritabanındaki unique index'tir; ihlal 500 değil, normal "zaten kayıtlı" cevabıdır (409).
        catch (UniqueConstraintViolationException)
        {
            throw new EmailAlreadyRegisteredException("Bu e-posta zaten kayıtlı.");
        }
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

    /// <summary>
    /// Tek tıkla demo girişi: her çağrıda yeni, tek kullanımlık misafir hesabı (rol: User) açar.
    /// Sabit bir "demo" kullanıcısı/parolası olsaydı repoda herkesin bildiği bir kimlik bilgisi olurdu ve
    /// ziyaretçiler birbirinin rezervasyonlarını görürdü. Parola özeti, kimsenin bilmediği rastgele bir
    /// değerin özetidir (<see cref="IPasswordHasher.DummyHash"/>): bu hesaba parolayla girilemez, yalnızca bu yanıttaki token çalışır.
    /// </summary>
    public async Task<AuthResult> CreateDemoSessionAsync(CancellationToken ct)
    {
        if (!demo.Value.Enabled) throw new NotFoundException("Demo girişi bu sunucuda kapalı.");

        var user = User.Create(DemoAccounts.NewGuestEmail(), hasher.DummyHash, UserRole.User, time.GetUtcNow());
        await users.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);
        return ToResult(user);
    }

    private AuthResult ToResult(User user)
    {
        var token = tokens.Create(user);
        return new AuthResult(token.Value, token.ExpiresAt, user.Id, user.Email, user.Role);
    }
}
