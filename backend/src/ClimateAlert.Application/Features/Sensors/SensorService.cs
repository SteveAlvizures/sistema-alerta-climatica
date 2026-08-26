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
        string name = BuildName(request.MeasurementType, location);
        string prefix = $"SEN-{CommunityAbbreviation(community.Name)}-{VariableAbbreviation(request.MeasurementType)}-";
        IReadOnlyList<string> existingCodes = await sensors.GetCodesAsync(request.CommunityId, prefix, cancellationToken);
        int sequence = existingCodes.Select(code => ParseSequence(code, prefix)).DefaultIfEmpty(0).Max() + 1;
        string code = $"{prefix}{sequence:00}";

        Sensor sensor;
        try
        {
            sensor = new Sensor(community, code, name, request.MeasurementType,
                SensorOrigin.Simulated, location, timeProvider.GetUtcNow());
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
            sensor.UpdateLocation(BuildName(sensor.MeasurementType, location), location);
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
        _ => throw new ValidationException("La variable climática no es válida.")
    };

    private static string VariableAbbreviation(ClimateVariable variable) => variable switch
    {
        ClimateVariable.Temperature => "TEMP",
        ClimateVariable.RelativeHumidity => "HUM",
        ClimateVariable.WindSpeed => "WIND",
        ClimateVariable.RainfallLevel => "RAIN",
        ClimateVariable.RiverOrReservoirLevel => "RIVER",
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

    private static SensorResponse Map(Sensor sensor) => new(
        sensor.Id, sensor.CommunityId, sensor.Code, sensor.Name, sensor.MeasurementType,
        sensor.Origin, sensor.Status, sensor.Location, sensor.DeviceCode,
        sensor.LastCommunicationAt, sensor.CreatedAt);
}
