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
    TimeProvider timeProvider) : ISensorReadingRegistrar
{
    public async Task<PagedResponse<SensorReadingResponse>> GetHistoryAsync(
        Guid sensorId, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        if (await sensors.GetByIdAsync(sensorId, false, cancellationToken) is null)
        {
            throw new NotFoundException("El sensor solicitado no existe.");
        }

        if (pageIndex < 1) throw new ValidationException("La página debe ser mayor o igual a 1.");
        if (pageSize < 1 || pageSize > 100) throw new ValidationException("El tamaño de página debe estar entre 1 y 100.");
        var result = await readings.GetPageBySensorAsync(sensorId, pageIndex, pageSize, cancellationToken);
        int totalPages = result.TotalCount == 0 ? 0 : (int)Math.Ceiling(result.TotalCount / (double)pageSize);
        return new(result.Items.Select(Map).ToList(), pageIndex, pageSize, totalPages, result.TotalCount,
            pageIndex > 1, pageIndex < totalPages);
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

    public async Task<SensorReadingResponse> CreateManualAsync(
        CreateManualSensorReadingRequest request, CancellationToken cancellationToken)
    {
        Sensor sensor = await sensors.GetByIdAsync(request.SensorId, false, cancellationToken)
            ?? throw new NotFoundException("El sensor solicitado no existe.");
        if (!request.Value.HasValue)
            throw new ValidationException("El valor de la lectura es obligatorio.");
        DateTimeOffset measuredAt = timeProvider.GetUtcNow();
        return await CreateAsync(new CreateSensorReadingRequest(
            sensor.Id, sensor.MeasurementType, request.Value.Value, UnitFor(sensor.MeasurementType),
            measuredAt, sensor.Origin), cancellationToken);
    }

    public static string UnitFor(ClimateVariable variable) => variable switch
    {
        ClimateVariable.Temperature => "°C",
        ClimateVariable.RelativeHumidity => "%",
        ClimateVariable.WindSpeed => "km/h",
        ClimateVariable.RainfallLevel => "mm",
        ClimateVariable.RiverOrReservoirLevel => "m",
        _ => throw new ValidationException("La variable climática no es válida.")
    };

    private static SensorReadingResponse Map(SensorReading reading) => new(
        reading.Id, reading.SensorId, reading.Variable, reading.Value, reading.Unit,
        reading.MeasuredAt, reading.ReceivedAt, reading.Origin);
}
