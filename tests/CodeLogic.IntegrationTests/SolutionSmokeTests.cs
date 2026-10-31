namespace CodeLogic.IntegrationTests;

public sealed class SolutionSmokeTests
{
    [Fact]
    public void TestProjectHasExpectedAssemblyName()
    {
        var assemblyName = typeof(SolutionSmokeTests).Assembly.GetName().Name;

        Assert.Equal("CodeLogic.IntegrationTests", assemblyName);
    }
}
