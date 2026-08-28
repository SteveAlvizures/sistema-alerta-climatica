using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Infrastructure.Simulation;

public sealed class SimulatedReadingValueGenerator : ISimulatedReadingValueGenerator
{
    private readonly object _sync = new();
    private readonly Dictionary<ClimateVariable, decimal> _values = [];
    private int _rainPhase;

    public SimulatedReadingValue Generate(ClimateVariable variable)
    {
        lock (_sync)
        {
            decimal previous = _values.GetValueOrDefault(variable, Baseline(variable));
            decimal target = Target(variable, DateTimeOffset.Now.Hour);
            decimal value = Clamp(variable, previous + (target - previous) * .18m + Noise(variable));
            _values[variable] = value;
            return new(decimal.Round(value, variable == ClimateVariable.RiverOrReservoirLevel ? 2 : 1), Unit(variable));
        }
    }

    private decimal Target(ClimateVariable variable, int hour) => variable switch
    {
        ClimateVariable.Temperature => 21m + 8m * (decimal)Math.Max(0, Math.Sin((hour - 6) * Math.PI / 14)),
        ClimateVariable.RelativeHumidity => 82m - (_values.GetValueOrDefault(ClimateVariable.Temperature, 23m) - 21m) * 1.5m,
        ClimateVariable.WindSpeed => hour is >= 11 and <= 17 ? 16m : 9m,
        ClimateVariable.RainfallLevel => RainTarget(),
        ClimateVariable.RiverOrReservoirLevel => .9m + _values.GetValueOrDefault(ClimateVariable.RainfallLevel) * .018m,
        _ => throw new ArgumentOutOfRangeException(nameof(variable), variable, null)
    };

    private decimal RainTarget()
    {
        if (_rainPhase == 0 && Random.Shared.NextDouble() < .025) _rainPhase = 1;
        if (_rainPhase > 0) _rainPhase = (_rainPhase + 1) % 18;
        return _rainPhase switch { 0 => 0m, <= 5 => _rainPhase * 2.5m,
            <= 10 => 15m + (_rainPhase - 5) * 2m, _ => Math.Max(0m, 25m - (_rainPhase - 10) * 3.5m) };
    }

    private static decimal Noise(ClimateVariable variable) => ((decimal)Random.Shared.NextDouble() - .5m) *
        (variable switch { ClimateVariable.Temperature => .6m, ClimateVariable.RelativeHumidity => 1.6m,
            ClimateVariable.WindSpeed => 2m, ClimateVariable.RainfallLevel => 1m, _ => .03m });
    private static decimal Baseline(ClimateVariable variable) => variable switch
    { ClimateVariable.Temperature => 22m, ClimateVariable.RelativeHumidity => 80m,
      ClimateVariable.WindSpeed => 10m, ClimateVariable.RainfallLevel => 0m,
      ClimateVariable.RiverOrReservoirLevel => .9m, _ => 0m };
    private static decimal Clamp(ClimateVariable variable, decimal value) => variable switch
    { ClimateVariable.Temperature => Math.Clamp(value, 12m, 42m), ClimateVariable.RelativeHumidity => Math.Clamp(value, 30m, 100m),
      ClimateVariable.WindSpeed => Math.Clamp(value, 0m, 65m), ClimateVariable.RainfallLevel => Math.Clamp(value, 0m, 60m),
      ClimateVariable.RiverOrReservoirLevel => Math.Clamp(value, .2m, 4m), _ => value };
    private static string Unit(ClimateVariable variable) => variable switch
    { ClimateVariable.Temperature => "°C", ClimateVariable.RelativeHumidity => "%", ClimateVariable.WindSpeed => "km/h",
      ClimateVariable.RainfallLevel => "mm", ClimateVariable.RiverOrReservoirLevel => "m", _ => string.Empty };
}
