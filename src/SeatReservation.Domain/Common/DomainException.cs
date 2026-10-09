namespace SeatReservation.Domain.Common;

/// <summary>
/// İş kuralı ihlali. Api katmanı alt tiplere bakarak HTTP durum kodunu seçer;
/// domain HTTP'yi bilmez, sadece "ne yanlış gitti"yi tipiyle söyler.
/// </summary>
public abstract class DomainException(string message) : Exception(message);

/// <summary>Geçersiz girdi (boş isim, negatif koltuk sayısı...). → 400</summary>
public sealed class DomainValidationException(string message) : DomainException(message);

/// <summary>Koltuk başkasının tutuşunda ya da satılmış. → 409</summary>
public sealed class SeatNotAvailableException(string message) : DomainException(message);

/// <summary>Tutma süresi dolmuş. → 409</summary>
public sealed class HoldExpiredException(string message) : DomainException(message);

/// <summary>Tutmanın sahibi olmayan biri onaylamaya çalıştı. → 403</summary>
public sealed class NotHoldOwnerException(string message) : DomainException(message);

/// <summary>Durum makinesinin izin vermediği geçiş (örn. iki kez onaylama). → 409</summary>
public sealed class InvalidStateTransitionException(string message) : DomainException(message);
