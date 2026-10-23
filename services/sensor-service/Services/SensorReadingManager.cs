using Microsoft.EntityFrameworkCore;
using SensorService.Data;
using SensorService.Domain;
using SensorService.Dtos;
using SensorService.Integrations;

namespace SensorService.Services;

public sealed class SensorReadingManager(
    SensorDbContext db,
    AlertClient alerts,
    CommunityClient communities,
    ILogger<SensorReadingManager> logger)
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
                    x => x.Id == request.SensorId,
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

        try
        {
            await alerts.EvaluateAsync(
                reading,
                authorization,
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "La lectura {ReadingId} fue guardada, pero Alert Service no pudo evaluarla.",
                reading.Id);
        }

        return Map(reading);
    }

    public async Task<PagedResponse<HistoryReadingResponse>>
        GetGlobalHistoryAsync(
            Guid? communityId,
            Guid? sensorId,
            ClimateVariable? variable,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            int page,
            int pageSize,
            string? authorization,
            CancellationToken cancellationToken)
    {
        if (page < 1)
            throw new ArgumentException(
                "La página debe ser mayor o igual a 1.");

        if (pageSize < 1 ||
            pageSize > 100)
            throw new ArgumentException(
                "El tamaño de página debe estar entre 1 y 100.");

        if (dateFrom.HasValue &&
            dateTo.HasValue &&
            dateFrom.Value > dateTo.Value)
            throw new ArgumentException(
                "La fecha inicial no puede ser mayor que la fecha final.");

        IQueryable<SensorReading> query =
            db.SensorReadings
                .AsNoTracking()
                .Include(x => x.Sensor);

        if (communityId.HasValue)
        {
            query = query.Where(
                x =>
                    x.Sensor.CommunityId ==
                    communityId.Value);
        }

        if (sensorId.HasValue)
        {
            query = query.Where(
                x =>
                    x.SensorId ==
                    sensorId.Value);
        }

        if (variable.HasValue)
        {
            query = query.Where(
                x =>
                    x.Variable ==
                    variable.Value);
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(
                x =>
                    x.MeasuredAt >=
                    dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(
                x =>
                    x.MeasuredAt <=
                    dateTo.Value);
        }

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

        Dictionary<Guid, string> communityNames =
            new();

        foreach (Guid id in data
            .Select(x => x.Sensor.CommunityId)
            .Distinct())
        {
            CommunityClient.CommunityResponse? community =
                await communities.GetByIdAsync(
                    id,
                    authorization,
                    cancellationToken);

            communityNames[id] =
                community?.Name ??
                string.Empty;
        }

        List<HistoryReadingResponse> result =
            data
                .Select(reading =>
                    new HistoryReadingResponse(
                        reading.Id,
                        reading.SensorId,
                        reading.Sensor.CommunityId,
                        reading.Sensor.Name,
                        reading.Sensor.Code,
                        communityNames[
                            reading.Sensor.CommunityId],
                        reading.Variable,
                        reading.Value,
                        reading.Unit,
                        reading.MeasuredAt,
                        reading.ReceivedAt,
                        reading.Origin,
                        reading.SensorStatusAtMeasurement))
                .ToList();

        int pages =
            total == 0
                ? 0
                : (int)Math.Ceiling(
                    total /
                    (double)pageSize);

        return new(
            result,
            page,
            pageSize,
            pages,
            total,
            page > 1,
            page < pages);
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
