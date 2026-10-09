namespace SeatReservation.Application.Demo;

public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    /// <summary>
    /// Demo modu: açılışta örnek etkinlikler tohumlanır ve <c>POST /api/auth/demo</c> tek tıkla misafir hesabı açar.
    /// Varsayılan kapalı; yalnızca vitrin/demo dağıtımında ortam değişkeniyle (Demo__Enabled=true) açılır.
    /// </summary>
    public bool Enabled { get; set; }
}
