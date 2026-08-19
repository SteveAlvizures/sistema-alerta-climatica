using System.Reflection;

namespace ClimateAlert.Domain.Tests;

public sealed class ArchitectureSmokeTests
{
    [Fact]
    public void DomainAssemblyCanBeLoaded()
    {
        Assembly assembly = Assembly.Load("ClimateAlert.Domain");

        Assert.Equal("ClimateAlert.Domain", assembly.GetName().Name);
    }
}
