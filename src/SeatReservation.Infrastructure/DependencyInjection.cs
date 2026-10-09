using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeatReservation.Application.Abstractions;
using SeatReservation.Infrastructure.Persistence;
using SeatReservation.Infrastructure.Persistence.Repositories;

namespace SeatReservation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Bağlantı dizesi tembel okunur (IConfiguration servis çözülürken): testler ve ortam değişkenleri
        // host kurulduktan sonra eklenen yapılandırmayı da görebilsin.
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                     ?? throw new InvalidOperationException("ConnectionStrings:Default tanımlı değil (ortam değişkeni: ConnectionStrings__Default).");
            // SQL Server konteyneri açılırken geçici bağlantı hatalarını yeniden dener.
            options.UseSqlServer(cs, sql => sql.EnableRetryOnFailure());
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<ISeatRepository, SeatRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        return services;
    }
}
