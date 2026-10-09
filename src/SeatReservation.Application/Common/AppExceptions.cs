namespace SeatReservation.Application.Common;

public abstract class AppException(string message) : Exception(message);

/// <summary>İstenen kayıt yok. → 404</summary>
public sealed class NotFoundException(string message) : AppException(message);

/// <summary>Girdi geçersiz (parola çok kısa, e-posta kayıtlı...). → 400 / 409</summary>
public sealed class RequestValidationException(string message) : AppException(message);

public sealed class EmailAlreadyRegisteredException(string message) : AppException(message);

/// <summary>
/// Veritabanı benzersizlik kuralı (unique index) ihlal edildi. Altyapı katmanı, sağlayıcıya özgü hatayı
/// (SQL Server / SQLite) bu tipe çevirir; servisler "hangi alan?" sorusunu kendi dillerinde cevaplar.
/// </summary>
public sealed class UniqueConstraintViolationException(string message, Exception? inner = null)
    : AppException(message)
{
    public Exception? Inner { get; } = inner;
}

/// <summary>Kullanıcı başına aktif koltuk sınırı aşıldı. → 409</summary>
public sealed class ReservationLimitExceededException(string message) : AppException(message);

/// <summary>E-posta/parola eşleşmedi. Hangisinin yanlış olduğu bilerek söylenmez. → 401</summary>
public sealed class InvalidCredentialsException() : AppException("E-posta veya parola hatalı.");

/// <summary>
/// Aynı kayıt, biz okuduktan sonra başkası tarafından değiştirildi (RowVersion uyuşmadı).
/// Kaybeden isteğin işi hiç yazılmaz. → 409
/// </summary>
public sealed class ConcurrencyConflictException(string message, Exception? inner = null)
    : AppException(message)
{
    public Exception? Inner { get; } = inner;
}
