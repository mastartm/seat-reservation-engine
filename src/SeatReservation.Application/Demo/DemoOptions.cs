namespace SeatReservation.Application.Demo;

public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    /// <summary>
    /// Demo modu: açılışta örnek etkinlikler tohumlanır ve <c>POST /api/auth/demo</c> tek tıkla misafir hesabı açar.
    /// Varsayılan kapalı; yalnızca vitrin/demo dağıtımında ortam değişkeniyle (Demo__Enabled=true) açılır.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Demo girişinin dakikada en çok kaç misafir hesabı açabileceği (tüm istemciler toplamı; aşılınca 429).
    /// İstemci başına değil genel sınır: proxy arkasında gerçek IP'ye güvenmek ayrıca yapılandırma gerektirir.
    /// Amaç, bir döngünün veritabanını misafirlerle doldurmasını yavaşlatmaktır. 0'dan büyük olmalı.
    /// </summary>
    public int MaxSessionsPerMinute { get; set; } = 30;

    /// <summary>
    /// Demo modunda misafirlerin onayladığı koltuğun serbest kalacağı süre (onaydan itibaren). Böylece biri tüm koltukları
    /// alsa bile demo kendini toparlar. Tohum veri etkilenmez. Sıfır ya da negatif: temizlik kapalı.
    /// </summary>
    public TimeSpan GuestSaleLifetime { get; set; } = TimeSpan.FromMinutes(30);
}
