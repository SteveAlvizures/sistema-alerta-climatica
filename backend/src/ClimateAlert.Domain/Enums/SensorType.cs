namespace ClimateAlert.Domain.Enums;

public enum SensorType
{
    Temperature, Humidity, WindSpeed, Rainfall, RiverLevel, ReservoirLevel, SmokeFire, OtherEnvironmental
}

public static class SensorTypes
{
    public static ClimateVariable VariableFor(SensorType type) => type switch
    {
        SensorType.Temperature => ClimateVariable.Temperature,
        SensorType.Humidity => ClimateVariable.RelativeHumidity,
        SensorType.WindSpeed => ClimateVariable.WindSpeed,
        SensorType.Rainfall => ClimateVariable.RainfallLevel,
        SensorType.RiverLevel or SensorType.ReservoirLevel => ClimateVariable.RiverOrReservoirLevel,
        SensorType.SmokeFire => ClimateVariable.SmokeConcentration,
        SensorType.OtherEnvironmental => ClimateVariable.OtherEnvironmental,
        _ => throw new ArgumentException("Invalid sensor type.")
    };

    public static SensorType TypeFor(ClimateVariable variable) => variable switch
    {
        ClimateVariable.Temperature => SensorType.Temperature,
        ClimateVariable.RelativeHumidity => SensorType.Humidity,
        ClimateVariable.WindSpeed => SensorType.WindSpeed,
        ClimateVariable.RainfallLevel => SensorType.Rainfall,
        ClimateVariable.RiverOrReservoirLevel => SensorType.RiverLevel,
        ClimateVariable.SmokeConcentration => SensorType.SmokeFire,
        ClimateVariable.OtherEnvironmental => SensorType.OtherEnvironmental,
        _ => throw new ArgumentException("Invalid climate variable.")
    };
}
