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
            services.AddDbContext<FleetCardsDbContext>(options =>
                options.UseSqlServer(connectionString)
            );
            services.AddSingleton<ICardProvider, MockCardProvider>();
            services.AddSingleton<MockFuelTransactionGenerator>();
            services.AddScoped<IFleetCardService, EfFleetCardService>();
            services.AddScoped<IFleetCardsReadService, EfFleetCardsReadService>();
            services.AddScoped<IFleetVehicleService, EfFleetVehicleService>();
            services.AddScoped<IAuthenticationService, EfAuthenticationService>();
            return services;
        }

        services.AddSingleton<SeededFleetVehicleService>();
        services.AddSingleton<IFleetVehicleService>(serviceProvider =>
            serviceProvider.GetRequiredService<SeededFleetVehicleService>()
        );
        services.AddSingleton<ICardProvider, MockCardProvider>();
        services.AddSingleton<MockFuelTransactionGenerator>();
        services.AddSingleton<IFleetCardService, SeededFleetCardService>();
        services.AddSingleton<IFleetCardsReadService, SeededFleetCardsReadService>();
        services.AddSingleton<IAuthenticationService, SeededAuthenticationService>();
        return services;
    }
}
