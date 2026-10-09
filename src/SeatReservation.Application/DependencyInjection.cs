using Microsoft.Extensions.DependencyInjection;
using SeatReservation.Application.Auth;
using SeatReservation.Application.Events;

namespace SeatReservation.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<EventService>();
        return services;
    }
}
