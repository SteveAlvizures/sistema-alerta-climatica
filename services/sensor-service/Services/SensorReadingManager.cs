using Microsoft.EntityFrameworkCore;
using SensorService.Data;
using SensorService.Domain;
using SensorService.Dtos;
using SensorService.Integrations;

namespace SensorService.Services;

public sealed class SensorReadingManager(
    SensorDbContext db,
    AlertClient alerts)
{
    public async Task<SensorReadingResponse>
        CreateManualAsync(
            CreateManualSensorReadingRequest request,
            string? authorization,
            CancellationToken cancellationToken)
    {
        if (!request.Value.HasValue)
            throw new ArgumentException(
                "El valor de la lectura es obligatorio.");

        Sensor sensor =
            await db.Sensors
                .FirstOrDefaultAsync(
                    x => x.Id ==
                        request.SensorId,
                    cancellationToken)
            ?? throw new KeyNotFoundException(
                "El sensor solicitado no existe.");

        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        SensorReading reading =
            new(
                sensor,
                request.Value.Value,
                now,
                now);

        sensor.UpdateLastCommunication(
            now);

        db.SensorReadings.Add(
            reading);

        await db.SaveChangesAsync(
            cancellationToken);

        await alerts.EvaluateAsync(
            reading,
            authorization,
            cancellationToken);

        return Map(reading);
    }

    public async Task<PagedResponse<SensorReadingResponse>>
        GetHistoryAsync(
            Guid sensorId,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
    {
        if (!await db.Sensors.AnyAsync(
                x => x.Id == sensorId,
                cancellationToken))
            throw new KeyNotFoundException(
                "El sensor solicitado no existe.");

        if (page < 1)
            throw new ArgumentException(
                "La página debe ser mayor o igual a 1.");

        if (pageSize < 1 ||
            pageSize > 100)
            throw new ArgumentException(
                "El tamaño de página debe estar entre 1 y 100.");

        IQueryable<SensorReading> query =
            db.SensorReadings
                .AsNoTracking()
                .Where(x =>
                    x.SensorId == sensorId);

        int total =
            await query.CountAsync(
                cancellationToken);

        List<SensorReading> data =
            await query
                .OrderByDescending(
                    x => x.MeasuredAt)
                .Skip(
                    (page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(
                    cancellationToken);

        int pages =
            total == 0
                ? 0
                : (int)Math.Ceiling(
                    total /
                    (double)pageSize);

        return new(
            data.Select(Map).ToList(),
            page,
            pageSize,
            pages,
            total,
            page > 1,
            page < pages);
    }

    public async Task<SensorReadingResponse>
        GetLatestAsync(
            Guid sensorId,
            CancellationToken cancellationToken)
    {
        SensorReading reading =
            await db.SensorReadings
                .AsNoTracking()
                .Where(x =>
                    x.SensorId == sensorId)
                .OrderByDescending(
                    x => x.MeasuredAt)
                .FirstOrDefaultAsync(
                    cancellationToken)
            ?? throw new KeyNotFoundException(
                "El sensor todavía no tiene lecturas.");

        return Map(reading);
    }

    private static SensorReadingResponse Map(
        SensorReading reading) =>
        new(
            reading.Id,
            reading.SensorId,
            reading.Variable,
            reading.Value,
            reading.Unit,
            reading.MeasuredAt,
            reading.ReceivedAt,
            reading.Origin,
            reading.SensorStatusAtMeasurement);
}
