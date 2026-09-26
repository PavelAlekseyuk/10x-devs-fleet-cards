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
        var vatRate =
            configuration.GetValue<decimal?>("Transactions:VatRate")
            ?? MockFuelTransactionGenerator.DefaultVatRate;
        var timeZoneId =
            configuration["Transactions:TimeZoneId"]
            ?? TransactionReportingService.DefaultTimeZoneId;
        services.AddSingleton<ITransactionReportingService>(
            new TransactionReportingService(timeZoneId)
        );
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<FleetCardsDbContext>(options =>
                options.UseSqlServer(connectionString)
            );
            services.AddSingleton<ICardProvider, MockCardProvider>();
            services.AddSingleton(new MockFuelTransactionGenerator(vatRate));
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
        services.AddSingleton(new MockFuelTransactionGenerator(vatRate));
        services.AddSingleton<IFleetCardService, SeededFleetCardService>();
        services.AddScoped<IFleetCardsReadService, SeededFleetCardsReadService>();
        services.AddSingleton<IAuthenticationService, SeededAuthenticationService>();
        return services;
    }
}
