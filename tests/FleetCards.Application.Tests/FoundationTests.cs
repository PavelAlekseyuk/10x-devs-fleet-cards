namespace FleetCards.Application.Tests;

public sealed class FoundationTests
{
    [Fact]
    public void Application_project_is_loadable()
    {
        Assert.NotNull(typeof(FleetCards.Application.Tests.FoundationTests).Assembly);
    }
}
