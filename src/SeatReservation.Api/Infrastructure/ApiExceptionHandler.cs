using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SeatReservation.Application.Common;
using SeatReservation.Domain.Common;

namespace SeatReservation.Api.Infrastructure;

/// <summary>
/// Domain/Application istisnalarını tek yerde HTTP'ye çevirir; controller'larda try/catch yok.
/// Bilinmeyen istisna 500 olur ve ayrıntısı istemciye sızmaz.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            DomainValidationException or RequestValidationException => (StatusCodes.Status400BadRequest, "Geçersiz istek"),
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized, "Kimlik doğrulanamadı"),
            NotHoldOwnerException => (StatusCodes.Status403Forbidden, "Yetkisiz"),
            NotFoundException => (StatusCodes.Status404NotFound, "Bulunamadı"),
            SeatNotAvailableException or HoldExpiredException or InvalidStateTransitionException
                or EmailAlreadyRegisteredException or ConcurrencyConflictException
                => (StatusCodes.Status409Conflict, "Çakışma"),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen hata"),
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "İşlenmeyen istisna");

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message,
        }, ct);
        return true;
    }
}
