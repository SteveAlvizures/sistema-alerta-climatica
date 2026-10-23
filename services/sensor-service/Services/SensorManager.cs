using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SensorService.Data;
using SensorService.Domain;
using SensorService.Dtos;
using SensorService.Integrations;

namespace SensorService.Services;

public sealed class SensorManager(
    SensorDbContext db,
    CommunityClient communities)
{
    public async Task<IReadOnlyList<SensorResponse>>
        GetAllAsync(
            CancellationToken cancellationToken)
    {
        List<Sensor> sensors =
            await db.Sensors
                .OrderBy(x => x.Code)
                .ToListAsync(cancellationToken);

        return sensors.Select(Map).ToList();
    }

    public async Task<PagedResponse<SensorResponse>>
        GetPageAsync(
            Guid? communityId,
            SensorType? type,
            ClimateVariable? variable,
            bool? isActive,
            string? code,
            string? search,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
    {
        if (page < 1 ||
            pageSize is < 1 or > 100)
            throw new ArgumentException(
                "La pagina debe ser positiva y el tamano debe estar entre 1 y 100.");

        IQueryable<Sensor> query =
            db.Sensors.AsNoTracking();

        if (communityId.HasValue)
            query = query.Where(x =>
                x.CommunityId ==
                communityId.Value);

        if (type.HasValue)
            query = query.Where(x =>
                x.Type == type.Value);

        if (variable.HasValue)
            query = query.Where(x =>
                x.MeasurementType ==
                variable.Value);

        if (isActive.HasValue)
        {
            SensorStatus expected =
                isActive.Value
                    ? SensorStatus.Active
                    : SensorStatus.Inactive;

            query = query.Where(x =>
                x.Status == expected);
        }

        if (!string.IsNullOrWhiteSpace(code))
            query = query.Where(x =>
                x.Code.Contains(code));

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x =>
                x.Code.Contains(search) ||
                x.Name.Contains(search) ||
                x.Location.Contains(search));

        int total =
            await query.CountAsync(
                cancellationToken);

        List<Sensor> items =
            await query
                .OrderBy(x => x.Code)
                .Skip((page - 1) * pageSize)
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
            items.Select(Map).ToList(),
            page,
            pageSize,
            pages,
            total,
            page > 1,
            page < pages);
    }

    public async Task<SensorResponse>
        GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
    {
        Sensor sensor =
            await db.Sensors
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken)
            ?? throw new KeyNotFoundException(
                "El sensor solicitado no existe.");

        return Map(sensor);
    }

    public async Task<IReadOnlyList<SensorResponse>>
        GetByCommunityAsync(
            Guid communityId,
            string? authorization,
            CancellationToken cancellationToken)
    {
        _ = await communities.GetByIdAsync(
                communityId,
                authorization,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "La comunidad solicitada no existe.");

        List<Sensor> sensors =
            await db.Sensors
                .AsNoTracking()
                .Where(x =>
                    x.CommunityId == communityId)
                .OrderBy(x => x.Code)
                .ToListAsync(
                    cancellationToken);

        return sensors.Select(Map).ToList();
    }

    public async Task<SensorResponse>
        CreateAsync(
            CreateSensorRequest request,
            string? authorization,
            CancellationToken cancellationToken)
    {
        CommunityClient.CommunityResponse community =
            await communities.GetByIdAsync(
                request.CommunityId,
                authorization,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "La comunidad solicitada no existe.");

        string location =
            ValidateLocation(request.Location);

        SensorType type =
            request.Type ??
            SensorTypes.TypeFor(
                request.MeasurementType);

        ClimateVariable variable =
            SensorTypes.VariableFor(type);

        if (request.Type.HasValue &&
            variable != request.MeasurementType)
        {
            throw new ArgumentException(
                "El tipo de sensor y la variable medida deben coincidir.");
        }

        string name =
            string.IsNullOrWhiteSpace(
                request.Name)
                ? BuildName(
                    variable,
                    location)
                : RequiredText(
                    request.Name,
                    150);

        string unit =
            string.IsNullOrWhiteSpace(
                request.Unit)
                ? UnitFor(variable)
                : RequiredText(
                    request.Unit,
                    30);

        ValidateDescription(
            request.Description);

        DateOnly installed =
            request.InstallationDate ??
            DateOnly.FromDateTime(
                DateTime.UtcNow);

        string prefix =
            $"SEN-{CommunityAbbreviation(community.Name)}-{VariableAbbreviation(variable)}-";

        List<string> codes =
            await db.Sensors
                .Where(x =>
                    x.CommunityId ==
                        request.CommunityId &&
                    x.Code.StartsWith(prefix))
                .Select(x => x.Code)
                .ToListAsync(
                    cancellationToken);

        int sequence =
            codes.Select(x =>
                    ParseSequence(
                        x,
                        prefix))
                .DefaultIfEmpty(0)
                .Max() + 1;

        string code =
            string.IsNullOrWhiteSpace(
                request.Code)
                ? $"{prefix}{sequence:00}"
                : RequiredText(
                    request.Code,
                    80);

        if (await db.Sensors.AnyAsync(
                x => x.Code == code,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Ya existe un sensor con ese codigo.");
        }

        Sensor sensor =
            new(
                request.CommunityId,
                code,
                name,
                variable,
                request.Origin,
                location,
                DateTimeOffset.UtcNow,
                request.DeviceCode,
                type,
                unit,
                installed,
                request.Description,
                request.IsActive);

        db.Sensors.Add(sensor);

        await db.SaveChangesAsync(
            cancellationToken);

        return Map(sensor);
    }

    public async Task<SensorResponse>
        ChangeStatusAsync(
            Guid id,
            bool active,
            CancellationToken cancellationToken)
    {
        Sensor sensor =
            await GetEntityAsync(
                id,
                cancellationToken);

        if (active)
            sensor.Activate();
        else
            sensor.Deactivate();

        await db.SaveChangesAsync(
            cancellationToken);

        return Map(sensor);
    }

    public async Task<SensorResponse>
        UpdateAsync(
            Guid id,
            UpdateSensorRequest request,
            string? authorization,
            CancellationToken cancellationToken)
    {
        Sensor sensor =
            await GetEntityAsync(
                id,
                cancellationToken);

        string location =
            ValidateLocation(
                request.Location);

        SensorType type =
            request.Type ??
            sensor.Type;

        ClimateVariable variable =
            SensorTypes.VariableFor(type);

        Guid communityId =
            request.CommunityId ??
            sensor.CommunityId;

        bool hasHistory =
            await db.SensorReadings.AnyAsync(
                x => x.SensorId == id,
                cancellationToken);

        if (hasHistory &&
            (communityId != sensor.CommunityId ||
             variable !=
             sensor.MeasurementType))
        {
            throw new InvalidOperationException(
                "No se puede cambiar la variable o comunidad de un sensor con lecturas o reglas asociadas.");
        }

        if (communityId !=
            sensor.CommunityId)
        {
            _ = await communities.GetByIdAsync(
                    communityId,
                    authorization,
                    cancellationToken)
                ?? throw new KeyNotFoundException(
                    "La comunidad solicitada no existe.");
        }

        string code =
            string.IsNullOrWhiteSpace(
                request.Code)
                ? sensor.Code
                : RequiredText(
                    request.Code,
                    80);

        if (await db.Sensors.AnyAsync(
                x =>
                    x.Id != id &&
                    x.Code == code,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Ya existe un sensor con ese codigo.");
        }

        string unit =
            string.IsNullOrWhiteSpace(
                request.Unit)
                ? sensor.Unit
                : RequiredText(
                    request.Unit,
                    30);

        if (hasHistory &&
            unit != sensor.Unit)
        {
            throw new InvalidOperationException(
                "No se puede cambiar la unidad de un sensor con lecturas o reglas asociadas.");
        }

        string name =
            string.IsNullOrWhiteSpace(
                request.Name)
                ? BuildName(
                    variable,
                    location)
                : RequiredText(
                    request.Name,
                    150);

        ValidateDescription(
            request.Description);

        sensor.Update(
            communityId,
            code,
            name,
            location,
            type,
            variable,
            unit,
            request.InstallationDate ??
                sensor.InstallationDate,
            request.Description ??
                sensor.Description,
            request.DeviceCode ??
                sensor.DeviceCode,
            request.IsActive);

        await db.SaveChangesAsync(
            cancellationToken);

        return Map(sensor);
    }

    private async Task<Sensor>
        GetEntityAsync(
            Guid id,
            CancellationToken cancellationToken)
    {
        return await db.Sensors
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "El sensor solicitado no existe.");
    }

    public static string UnitFor(
        ClimateVariable variable) =>
        variable switch
        {
            ClimateVariable.Temperature =>
                "°C",
            ClimateVariable.RelativeHumidity =>
                "%",
            ClimateVariable.WindSpeed =>
                "km/h",
            ClimateVariable.RainfallLevel =>
                "mm",
            ClimateVariable.RiverOrReservoirLevel =>
                "m",
            ClimateVariable.SmokeConcentration =>
                "ppm",
            ClimateVariable.OtherEnvironmental =>
                "u",
            _ => throw new ArgumentException(
                "La variable climática no es válida.")
        };

    private static SensorResponse Map(
        Sensor sensor) =>
        new(
            sensor.Id,
            sensor.CommunityId,
            sensor.Code,
            sensor.Name,
            sensor.MeasurementType,
            sensor.Origin,
            sensor.Status,
            sensor.Location,
            sensor.DeviceCode,
            sensor.LastCommunicationAt,
            sensor.CreatedAt,
            sensor.Type,
            sensor.Unit,
            sensor.InstallationDate,
            sensor.Description,
            null,
            sensor.IsActive);

    private static string BuildName(
        ClimateVariable variable,
        string location) =>
        $"Sensor de {VariableLabel(variable).ToLowerInvariant()} - {location.Trim()}";

    private static string VariableLabel(
        ClimateVariable variable) =>
        variable switch
        {
            ClimateVariable.Temperature =>
                "Temperatura",
            ClimateVariable.RelativeHumidity =>
                "Humedad relativa",
            ClimateVariable.WindSpeed =>
                "Velocidad del viento",
            ClimateVariable.RainfallLevel =>
                "Nivel de lluvia",
            ClimateVariable.RiverOrReservoirLevel =>
                "Nivel de río o reservorio",
            ClimateVariable.SmokeConcentration =>
                "Humo/incendio",
            ClimateVariable.OtherEnvironmental =>
                "Otro sensor ambiental",
            _ => throw new ArgumentException()
        };

    private static string VariableAbbreviation(
        ClimateVariable variable) =>
        variable switch
        {
            ClimateVariable.Temperature =>
                "TEMP",
            ClimateVariable.RelativeHumidity =>
                "HUM",
            ClimateVariable.WindSpeed =>
                "WIND",
            ClimateVariable.RainfallLevel =>
                "RAIN",
            ClimateVariable.RiverOrReservoirLevel =>
                "RIVER",
            ClimateVariable.SmokeConcentration =>
                "SMOKE",
            ClimateVariable.OtherEnvironmental =>
                "ENV",
            _ => throw new ArgumentException()
        };

    private static string CommunityAbbreviation(
        string name)
    {
        string normalized =
            name.Normalize(
                NormalizationForm.FormD);

        string clean =
            new(
                normalized
                    .Where(character =>
                        CharUnicodeInfo
                            .GetUnicodeCategory(
                                character)
                        != UnicodeCategory
                            .NonSpacingMark)
                    .ToArray());

        string[] words =
            clean.Split(
                ' ',
                StringSplitOptions
                    .RemoveEmptyEntries);

        return string.Concat(
            words.Take(3)
                .Select(word =>
                    char.ToUpperInvariant(
                        word[0])));
    }

    private static int ParseSequence(
        string code,
        string prefix) =>
        code.StartsWith(
            prefix,
            StringComparison.OrdinalIgnoreCase)
        && int.TryParse(
            code[prefix.Length..],
            out int sequence)
            ? sequence
            : 0;

    private static string ValidateLocation(
        string? location)
    {
        string value =
            location?.Trim() ??
            string.Empty;

        if (value.Length < 3 ||
            value.Length > 200)
            throw new ArgumentException(
                "La ubicación o referencia debe contener entre 3 y 200 caracteres.");

        return value;
    }

    private static string RequiredText(
        string value,
        int max)
    {
        if (string.IsNullOrWhiteSpace(
                value) ||
            value.Trim().Length > max)
        {
            throw new ArgumentException(
                "Falta un campo obligatorio o excede su longitud permitida.");
        }

        return value.Trim();
    }

    private static void ValidateDescription(
        string? value)
    {
        if (value?.Trim().Length > 1000)
            throw new ArgumentException(
                "La descripcion excede 1000 caracteres.");
    }
}
