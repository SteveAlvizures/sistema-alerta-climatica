using ClimateAlert.Infrastructure.Simulation;

namespace ClimateAlert.Infrastructure.Tests;

public sealed class SimulationConfigurationTests
{
    [Theory]
    [InlineData(null, 60)]
    [InlineData("invalid", 60)]
    [InlineData("15", 15)]
    [InlineData("0", 0)]
    [InlineData("-1", -1)]
    public void ReadsSimulationInterval(string? configured, int expected) =>
        Assert.Equal(expected, SimulatedReadingHostedService.ReadInterval(configured));
}
