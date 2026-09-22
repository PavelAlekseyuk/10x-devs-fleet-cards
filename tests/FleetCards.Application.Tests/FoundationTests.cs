using FleetCards.Contracts;
using FleetCards.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FleetCards.Application.Tests;

public sealed class FoundationTests
{
    [Fact]
    public void Application_project_is_loadable()
    {
        Assert.NotNull(typeof(FleetCards.Application.Tests.FoundationTests).Assembly);
    }

    [Theory]
    [InlineData("anna.nowak", "Anna123!", "ReadWrite")]
    [InlineData("piotr.wisniewski", "Piotr123!", "ReadOnly")]
    public void Seeded_users_validate_to_safe_current_user_records(
        string userName,
        string password,
        string permission
    )
    {
        var service = new SeededAuthenticationService();

        var result = service.ValidateCredentials(userName, password);

        Assert.Equal(new CurrentUserResponse(userName, permission), result);
    }

    [Theory]
    [InlineData("unknown.user", "Anna123!")]
    [InlineData("anna.nowak", "wrong-password")]
    public void Invalid_credentials_return_the_same_generic_result(string userName, string password)
    {
        var service = new SeededAuthenticationService();

        var result = service.ValidateCredentials(userName, password);

        Assert.Null(result);
    }

    [Fact]
    public void Ef_seeded_users_validate_from_persisted_hashes()
    {
        var options = new DbContextOptionsBuilder<FleetCardsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new FleetCardsDbContext(options);
        dbContext.Database.EnsureCreated();
        var service = new EfAuthenticationService(dbContext);

        var anna = service.ValidateCredentials("anna.nowak", "Anna123!");
        var piotr = service.ValidateCredentials("piotr.wisniewski", "Piotr123!");
        var invalid = service.ValidateCredentials("piotr.wisniewski", "wrong-password");

        Assert.Equal(new CurrentUserResponse("anna.nowak", "ReadWrite"), anna);
        Assert.Equal(new CurrentUserResponse("piotr.wisniewski", "ReadOnly"), piotr);
        Assert.Null(invalid);
        Assert.All(dbContext.Users, user => Assert.DoesNotContain("Anna123!", user.PasswordHash));
        Assert.All(dbContext.Users, user => Assert.DoesNotContain("Piotr123!", user.PasswordHash));
    }
}
