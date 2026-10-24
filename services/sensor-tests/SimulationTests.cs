using SensorSimulation;
public class SimulationTests
{
    [Theory]
    [InlineData(ClimateVariable.Temperature,12,42,"°C")]
    [InlineData(ClimateVariable.RelativeHumidity,30,100,"%")]
    [InlineData(ClimateVariable.WindSpeed,0,65,"km/h")]
    [InlineData(ClimateVariable.RainfallLevel,0,60,"mm")]
    [InlineData(ClimateVariable.RiverOrReservoirLevel,0.2,4,"m")]
    [InlineData(ClimateVariable.SmokeConcentration,0,100,"ppm")]
    [InlineData(ClimateVariable.OtherEnvironmental,0,100,"u")]
    public void ReusedGeneratorKeepsPhysicalBoundsAndUnits(ClimateVariable variable,double minimum,double maximum,string unit)
    {
        var generator=new SimulatedReadingValueGenerator();
        for(int i=0;i<1000;i++){var reading=generator.Generate(variable);Assert.InRange(reading.Value,(decimal)minimum,(decimal)maximum);Assert.Equal(unit,reading.Unit);}
    }
    [Fact] public void AdministrativeStateIsIndependentOfCommunication()
    {
        var sensor=new SensorRecord{Status="Inactive",LastCommunicationAt=DateTimeOffset.UtcNow};
        Assert.False(sensor.IsActive);sensor.Status="Active";Assert.True(sensor.IsActive);
    }
}
