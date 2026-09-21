using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FleetCards.Infrastructure;

public sealed class FleetCardsDbContextFactory : IDesignTimeDbContextFactory<FleetCardsDbContext>
{
    public FleetCardsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FleetCardsDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\MSSQLLocalDB;Database=FleetCardsDesign;Trusted_Connection=True;TrustServerCertificate=True"
        );

        return new FleetCardsDbContext(optionsBuilder.Options);
    }
}