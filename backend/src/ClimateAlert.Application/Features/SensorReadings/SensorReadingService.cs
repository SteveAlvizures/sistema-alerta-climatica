using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;

namespace ClimateAlert.Application.Features.SensorReadings;

public sealed class SensorReadingService(
    ISensorRepository sensors,
    ISensorReadingRepository readings,
    IAlertEvaluator alertEvaluator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<SensorReadingResponse>> GetHistoryAsync(
        Guid sensorId, int limit, CancellationToken cancellationToken)
    {
        if (await sensors.GetByIdAsync(sensorId, false, cancellationToken) is null)
        {
            throw new NotFoundException("El sensor solicitado no existe.");
        }

        int effectiveLimit = limit <= 0 ? 50 : Math.Min(limit, 100);
        return (await readings.GetBySensorAsync(sensorId, effectiveLimit, cancellationToken)).Select(Map).ToList();
    }

    public async Task<SensorReadingResponse> GetLatestAsync(Guid sensorId, CancellationToken cancellationToken)
    {
        if (await sensors.GetByIdAsync(sensorId, false, cancellationToken) is null)
        {
            throw new NotFoundException("El sensor solicitado no existe.");
        }

        SensorReading reading = await readings.GetLatestAsync(sensorId, cancellationToken)
            ?? throw new NotFoundException("El sensor todavía no tiene lecturas.");
        return Map(reading);
    }

    public async Task<SensorReadingResponse> CreateAsync(CreateSensorReadingRequest request, CancellationToken cancellationToken)
    {
        Sensor sensor = await sensors.GetByIdAsync(request.SensorId, true, cancellationToken)
            ?? throw new NotFoundException("El sensor solicitado no existe.");

        if (sensor.Status != SensorStatus.Active)
        {
            throw new ConflictException("El sensor debe estar activo para registrar lecturas.");
        }

        DateTimeOffset receivedAt = timeProvider.GetUtcNow();
        SensorReading reading;
        try
        {
            reading = new SensorReading(sensor, request.Variable, request.Value, request.Unit,
                request.MeasuredAt, receivedAt, request.Origin);
            sensor.UpdateLastCommunication(receivedAt);
        }
        catch (ArgumentException exception)
        {
            throw new ValidationException(exception.Message);
        }

        readings.Add(reading);
        await alertEvaluator.EvaluateAsync(reading, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(reading);
    }

    private static SensorReadingResponse Map(SensorReading reading) => new(
        reading.Id, reading.SensorId, reading.Variable, reading.Value, reading.Unit,
        reading.MeasuredAt, reading.ReceivedAt, reading.Origin);
}
