using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Infrastructure.Simulation;

public sealed class SimulatedReadingValueGenerator : ISimulatedReadingValueGenerator
{
    public SimulatedReadingValue Generate(ClimateVariable variable) => variable switch
    {
        ClimateVariable.Temperature => Create(-5m, 40m, "°C", 1),
        ClimateVariable.RelativeHumidity => Create(20m, 100m, "%", 1),
        ClimateVariable.WindSpeed => Create(0m, 80m, "km/h", 1),
        ClimateVariable.RainfallLevel => Create(0m, 100m, "mm", 1),
        ClimateVariable.RiverOrReservoirLevel => Create(0m, 10m, "m", 2),
        _ => throw new ArgumentOutOfRangeException(nameof(variable), variable, null)
    };

    private static SimulatedReadingValue Create(
        decimal minimum,
        decimal maximum,
        string unit,
        int decimals)
    {
        decimal value = minimum + (decimal)Random.Shared.NextDouble() * (maximum - minimum);
        return new SimulatedReadingValue(decimal.Round(value, decimals), unit);
    }
}
