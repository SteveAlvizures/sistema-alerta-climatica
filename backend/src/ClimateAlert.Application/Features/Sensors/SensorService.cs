using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Domain.Enums;
using System.Globalization;
using System.Text;

namespace ClimateAlert.Application.Features.Sensors;

public sealed class SensorService(
    ISensorRepository sensors,
    ICommunityRepository communities,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<ClimateAlert.Application.Features.SensorReadings.PagedResponse<SensorResponse>> GetPageAsync(
        Guid? communityId, SensorType? type, ClimateVariable? variable, bool? isActive, string? code, string? search,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ValidationException("La pagina debe ser positiva y el tamano debe estar entre 1 y 100.");
        if ((type.HasValue && !Enum.IsDefined(type.Value)) || (variable.HasValue && !Enum.IsDefined(variable.Value))) throw new ValidationException("El tipo o la variable del sensor no es valido.");
        var result = await sensors.GetPageAsync(communityId, type, variable, isActive, code, search, page, pageSize, cancellationToken);
        int pages = (int)Math.Ceiling(result.TotalCount / (double)pageSize);
        return new(result.Items.Select(Map).ToList(), page, pageSize, pages, result.TotalCount, page > 1, page < pages);
    }

    public async Task<IReadOnlyList<SensorResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        (await sensors.GetAllAsync(cancellationToken)).Select(Map).ToList();

    public async Task<IReadOnlyList<SensorResponse>> GetByCommunityAsync(Guid communityId, CancellationToken cancellationToken)
    {
        if (await communities.GetByIdAsync(communityId, false, cancellationToken) is null)
        {
            throw new NotFoundException("La comunidad solicitada no existe.");
        }

        return (await sensors.GetByCommunityAsync(communityId, cancellationToken)).Select(Map).ToList();
    }

    public async Task<SensorResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Sensor sensor = await sensors.GetByIdAsync(id, false, cancellationToken)
            ?? throw new NotFoundException("El sensor solicitado no existe.");
        return Map(sensor);
    }

    public async Task<SensorResponse> CreateAsync(CreateSensorRequest request, CancellationToken cancellationToken)
    {
        Community community = await communities.GetByIdAsync(request.CommunityId, true, cancellationToken)
            ?? throw new NotFoundException("La comunidad solicitada no existe.");

        string location = ValidateLocation(request.Location);
        if (!Enum.IsDefined(request.MeasurementType) || (request.Type.HasValue && !Enum.IsDefined(request.Type.Value))) throw new ValidationException("El tipo o la variable del sensor no es valido.");
        SensorType type = request.Type ?? SensorTypes.TypeFor(request.MeasurementType);
        if (request.Type.HasValue && request.MeasurementType != SensorTypes.VariableFor(type)) throw new ValidationException("El tipo de sensor y la variable medida deben coincidir.");
        ClimateVariable variable = SensorTypes.VariableFor(type);
        if (!Enum.IsDefined(request.Origin)) throw new ValidationException("El origen del sensor no es valido.");
        string name = request.Name is null ? BuildName(variable, location) : RequiredText(request.Name, 150);
        string unit = request.Unit is null ? ClimateAlert.Application.Features.SensorReadings.SensorReadingService.UnitFor(variable) : RequiredText(request.Unit, 30);
        ValidateDescription(request.Description);
        DateOnly installed = request.InstallationDate ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        ValidateInstallationDate(installed);
        string prefix = $"SEN-{CommunityAbbreviation(community.Name)}-{VariableAbbreviation(variable)}-";
        IReadOnlyList<string> existingCodes = await sensors.GetCodesAsync(request.CommunityId, prefix, cancellationToken);
        int sequence = existingCodes.Select(code => ParseSequence(code, prefix)).DefaultIfEmpty(0).Max() + 1;
        string code = request.Code is null ? $"{prefix}{sequence:00}" : RequiredText(request.Code, 80);
        if (request.Code is not null && await sensors.CodeExistsAsync(code, null, cancellationToken)) throw new ConflictException("Ya existe un sensor con ese codigo.");

        Sensor sensor;
        try
        {
            sensor = new Sensor(community, code, name, variable,
                request.Origin, location, timeProvider.GetUtcNow(), request.DeviceCode);
            sensor.Configure(type, unit, installed, request.Description);
            if (request.IsActive) sensor.Activate();
        }
        catch (ArgumentException exception)
        {
            throw new ValidationException(exception.Message);
        }

        sensors.Add(sensor);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(sensor);
    }

    public async Task<SensorResponse> ChangeStatusAsync(Guid id, ChangeSensorStatusRequest request, CancellationToken cancellationToken)
    {
        Sensor sensor = await sensors.GetByIdAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("El sensor solicitado no existe.");

        if (request.IsActive) sensor.Activate(); else sensor.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(sensor);
    }

    public async Task<SensorResponse> UpdateAsync(Guid id, UpdateSensorRequest request, CancellationToken cancellationToken)
    {
        Sensor sensor = await sensors.GetByIdAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("El sensor solicitado no existe.");
        try
        {
            string location = ValidateLocation(request.Location);
            if (request.Type.HasValue && !Enum.IsDefined(request.Type.Value)) throw new ValidationException("El tipo de sensor no es valido.");
            SensorType type = request.Type ?? sensor.Type ?? SensorTypes.TypeFor(sensor.MeasurementType);
            ClimateVariable variable = SensorTypes.VariableFor(type);
            Guid communityId = request.CommunityId ?? sensor.CommunityId;
            if (variable != sensor.MeasurementType || communityId != sensor.CommunityId)
            {
                if (await sensors.HasHistoryAsync(id, cancellationToken)) throw new ConflictException("No se puede cambiar la variable o comunidad de un sensor con lecturas o reglas asociadas.");
                Community community = await communities.GetByIdAsync(communityId, true, cancellationToken) ?? throw new NotFoundException("La comunidad solicitada no existe.");
                sensor.Reassign(community, variable);
            }
            string code = request.Code is null ? sensor.Code : RequiredText(request.Code, 80);
            if (request.Code is not null && code != sensor.Code && await sensors.CodeExistsAsync(code, id, cancellationToken)) throw new ConflictException("Ya existe un sensor con ese codigo.");
            string unit = request.Unit is null ? sensor.Unit ?? ClimateAlert.Application.Features.SensorReadings.SensorReadingService.UnitFor(variable) : RequiredText(request.Unit, 30);
            if (unit != (sensor.Unit ?? ClimateAlert.Application.Features.SensorReadings.SensorReadingService.UnitFor(sensor.MeasurementType)) && await sensors.HasHistoryAsync(id, cancellationToken)) throw new ConflictException("No se puede cambiar la unidad de un sensor con lecturas o reglas asociadas.");
            DateOnly? installed = request.InstallationDate ?? sensor.InstallationDate;
            if (installed.HasValue) ValidateInstallationDate(installed.Value); ValidateDescription(request.Description);
            sensor.UpdateAdministrativeDetails(code, request.Name is null ? BuildName(variable, location) : RequiredText(request.Name, 150), location, request.DeviceCode ?? sensor.DeviceCode);
            sensor.Configure(type, unit, installed, request.Description ?? sensor.Description);
            if (request.IsActive.HasValue) { if (request.IsActive.Value) sensor.Activate(); else sensor.Deactivate(); }
        }
        catch (ArgumentException exception)
        {
            throw new ValidationException(exception.Message);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(sensor);
    }

    public static string BuildName(ClimateVariable variable, string location) =>
        $"Sensor de {VariableLabel(variable).ToLowerInvariant()} - {location.Trim()}";

    public static string VariableLabel(ClimateVariable variable) => variable switch
    {
        ClimateVariable.Temperature => "Temperatura",
        ClimateVariable.RelativeHumidity => "Humedad relativa",
        ClimateVariable.WindSpeed => "Velocidad del viento",
        ClimateVariable.RainfallLevel => "Nivel de lluvia",
        ClimateVariable.RiverOrReservoirLevel => "Nivel de río o reservorio",
        ClimateVariable.SmokeConcentration => "Humo/incendio",
        ClimateVariable.OtherEnvironmental => "Otro sensor ambiental",
        _ => throw new ValidationException("La variable climática no es válida.")
    };

    private static string VariableAbbreviation(ClimateVariable variable) => variable switch
    {
        ClimateVariable.Temperature => "TEMP",
        ClimateVariable.RelativeHumidity => "HUM",
        ClimateVariable.WindSpeed => "WIND",
        ClimateVariable.RainfallLevel => "RAIN",
        ClimateVariable.RiverOrReservoirLevel => "RIVER",
        ClimateVariable.SmokeConcentration => "SMOKE",
        ClimateVariable.OtherEnvironmental => "ENV",
        _ => throw new ValidationException("La variable climática no es válida.")
    };

    private static string CommunityAbbreviation(string name)
    {
        Dictionary<string, string> official = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Lanquín"] = "LAN", ["Livingston"] = "LIV", ["San Juan La Laguna"] = "SJL",
            ["Santa Catarina Palopó"] = "SCP", ["San Juan Chamelco"] = "SJC",
            ["Todos Santos Cuchumatán"] = "TSC"
        };
        if (official.TryGetValue(name, out string? abbreviation)) return abbreviation;
        string normalized = name.Normalize(NormalizationForm.FormD);
        string[] words = new string(normalized.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(words.Take(3).Select(word => char.ToUpperInvariant(word[0])));
    }

    private static int ParseSequence(string code, string prefix) =>
        code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        && int.TryParse(code[prefix.Length..], out int sequence) ? sequence : 0;

    private static string ValidateLocation(string? location)
    {
        string value = location?.Trim() ?? string.Empty;
        if (value.Length < 3 || value.Length > 200)
            throw new ValidationException("La ubicación o referencia debe contener entre 3 y 200 caracteres.");
        return value;
    }

    private static string RequiredText(string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max) throw new ValidationException("Falta un campo obligatorio o excede su longitud permitida.");
        return value.Trim();
    }
    private static void ValidateInstallationDate(DateOnly date)
    {
        if (date == DateOnly.MinValue) throw new ValidationException("La fecha de instalacion es obligatoria.");
    }
    private static void ValidateDescription(string? value)
    {
        if (value?.Trim().Length > 1000) throw new ValidationException("La descripcion excede 1000 caracteres.");
    }

    private static SensorResponse Map(Sensor sensor) => new(
        sensor.Id, sensor.CommunityId, sensor.Code, sensor.Name, sensor.MeasurementType,
        sensor.Origin, sensor.Status, sensor.Location, sensor.DeviceCode,
        sensor.LastCommunicationAt, sensor.CreatedAt, sensor.Type ?? SensorTypes.TypeFor(sensor.MeasurementType),
        sensor.Unit ?? ClimateAlert.Application.Features.SensorReadings.SensorReadingService.UnitFor(sensor.MeasurementType),
        sensor.InstallationDate, sensor.Description, sensor.Community.Name, sensor.IsActive);
}
