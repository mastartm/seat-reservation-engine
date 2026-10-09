using Microsoft.Extensions.DependencyInjection;
using SeatReservation.Application.Auth;
using SeatReservation.Application.Events;
using SeatReservation.Application.Reservations;

namespace SeatReservation.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<EventService>();
        services.AddScoped<ReservationService>();
        services.AddScoped<HoldExpirationService>();
        services.AddScoped<Demo.DemoCleanupService>();
        services.AddOptions<ReservationOptions>();
        services.AddOptions<Demo.DemoOptions>();
        return services;
    }
}
