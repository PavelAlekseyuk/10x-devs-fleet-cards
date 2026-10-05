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
        services.AddScoped<ProgramComparisonService>();
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<FleetCardsDbContext>(options =>
                options.UseSqlServer(connectionString)
            );
            services.AddSingleton<ICardProvider, MockCardProvider>();
            services.AddScoped<IProgramResolutionDataSource, EfProgramResolutionDataSource>();
            services.AddScoped<IProgramResolutionService, ProgramResolutionService>();
            services.AddScoped<MockFuelTransactionGenerator>(serviceProvider =>
                new MockFuelTransactionGenerator(
                    vatRate,
                    serviceProvider.GetRequiredService<IProgramResolutionService>()
                )
            );
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
        services.AddSingleton<IProgramResolutionDataSource, SeededProgramResolutionDataSource>();
        services.AddSingleton<IProgramResolutionService, ProgramResolutionService>();
        services.AddSingleton<MockFuelTransactionGenerator>(serviceProvider =>
            new MockFuelTransactionGenerator(
                vatRate,
                serviceProvider.GetRequiredService<IProgramResolutionService>()
            )
        );
        services.AddSingleton<IFleetCardService, SeededFleetCardService>();
        services.AddScoped<IFleetCardsReadService, SeededFleetCardsReadService>();
        services.AddSingleton<IAuthenticationService, SeededAuthenticationService>();
        return services;
    }
}
