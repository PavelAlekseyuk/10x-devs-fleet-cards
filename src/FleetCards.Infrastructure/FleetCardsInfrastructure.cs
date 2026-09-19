using FleetCards.Application;

namespace FleetCards.Infrastructure;

public static class FleetCardsInfrastructure
{
    public static IServiceCollection AddFleetCardsInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IFleetCardsReadService, SeededFleetCardsReadService>();
        return services;
    }
}
