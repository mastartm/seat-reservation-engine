namespace SeatReservation.Infrastructure.Security;

/// <summary>Bölüm adı "Jwt". Anahtar yalnızca ortam değişkeninden gelir: <c>Jwt__Key</c>.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinKeyLength = 32; // HS256 için en az 256 bit

    public string Issuer { get; set; } = "seat-reservation";
    public string Audience { get; set; } = "seat-reservation";
    public string Key { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}
