using ClimateAlert.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace ClimateAlert.Infrastructure.Simulation;

public sealed class SimulationErrorReporter(
    ILogger<SimulationErrorReporter> logger) : ISimulationErrorReporter
{
    public void ReportSensorFailure(Guid sensorId, Exception exception) =>
        logger.LogError(exception, "Could not generate a simulated reading for sensor {SensorId}.", sensorId);
}
