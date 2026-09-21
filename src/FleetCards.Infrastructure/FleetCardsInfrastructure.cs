using FleetCards.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FleetCards.Infrastructure;

public static class FleetCardsInfrastructure
{
    public static IServiceCollection AddFleetCardsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString = configuration.GetConnectionString("fleetcards");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<FleetCardsDbContext>(options => options.UseSqlServer(connectionString));
            services.AddScoped<IFleetCardsReadService, EfFleetCardsReadService>();
            return services;
        }

        services.AddSingleton<IFleetCardsReadService, SeededFleetCardsReadService>();
        return services;
    }
}
