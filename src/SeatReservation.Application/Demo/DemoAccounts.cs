namespace SeatReservation.Application.Demo;

/// <summary>
/// Demo hesaplarının adlandırması tek yerde. Misafir: "misafir-xxxx@demo.local". Tohum verinin sahibi
/// ("tohum@demo.local") da aynı alan adını kullanır ama misafir DEĞİLDİR: onun koltukları temizlikte korunur.
/// </summary>
public static class DemoAccounts
{
    public const string GuestPrefix = "misafir-";
    public const string ReservedDomain = "@demo.local";

    public static string NewGuestEmail() => $"{GuestPrefix}{Guid.NewGuid().ToString("N")[..12]}{ReservedDomain}";

    /// <summary>Alan adı demo hesaplarına ayrılmıştır; herkese açık kayıtla bu alan adı alınamaz.</summary>
    public static bool IsReservedDomain(string email) => email.EndsWith(ReservedDomain, StringComparison.OrdinalIgnoreCase);
}
