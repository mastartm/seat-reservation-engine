using Microsoft.Extensions.DependencyInjection;
using SeatReservation.Application.Auth;

namespace SeatReservation.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        return services;
    }
}
