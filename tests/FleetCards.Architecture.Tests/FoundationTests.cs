namespace FleetCards.Architecture.Tests;

public sealed class FoundationTests
{
    [Fact]
    public void Architecture_test_project_is_loadable()
    {
        Assert.NotNull(typeof(FleetCards.Architecture.Tests.FoundationTests).Assembly);
    }
}
