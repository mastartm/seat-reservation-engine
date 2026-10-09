using System.Security.Cryptography;
using SeatReservation.Application.Abstractions;

namespace SeatReservation.Infrastructure.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 (BCL'de hazır; ek kütüphane gerekmez). Biçim: v1.{iterasyon}.{tuz}.{özet}.
/// İterasyon sayısı özetin içinde saklanır, böylece ileride artırılırsa eski kayıtlar doğrulanmaya devam eder.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 600_000; // OWASP 2023 önerisi (PBKDF2-SHA256)
    private const int SaltSize = 16;
    private const int KeySize = 32;

    private static readonly Lazy<string> Dummy = new(() => HashCore(Guid.NewGuid().ToString()));

    public string DummyHash => Dummy.Value;

    public string Hash(string password) => HashCore(password);

    private static string HashCore(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iterations)) return false;

        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        // Sabit zamanlı karşılaştırma: erken çıkan == zamanlama farkı üzerinden özet sızdırır.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
