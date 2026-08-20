using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Application.Features.SensorReadings;

public sealed class SimulatedReadingCycle(
    ISensorRepository sensors,
    ISimulatedReadingValueGenerator valueGenerator,
    ISensorReadingRegistrar readingRegistrar,
    ISimulationErrorReporter errorReporter,
    TimeProvider timeProvider) : ISimulatedReadingCycle
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Sensor> activeSensors =
            await sensors.GetActiveSimulatedAsync(cancellationToken);

        foreach (Sensor sensor in activeSensors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                SimulatedReadingValue generated = valueGenerator.Generate(sensor.MeasurementType);
                DateTimeOffset measuredAt = timeProvider.GetUtcNow();
                await readingRegistrar.CreateAsync(
                    new CreateSensorReadingRequest(
                        sensor.Id,
                        sensor.MeasurementType,
                        generated.Value,
                        generated.Unit,
                        measuredAt,
                        SensorOrigin.Simulated),
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                errorReporter.ReportSensorFailure(sensor.Id, exception);
            }
        }
    }
}
