using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;

namespace ClimateAlert.Application.Features.AlertRules;

public sealed class AlertRuleService(
    IAlertRuleRepository rules,
    ICommunityRepository communities,
    ISensorRepository sensors,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<AlertRuleResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        (await rules.GetAllAsync(cancellationToken)).Select(Map).ToList();

    public async Task<AlertRuleResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Map(await rules.GetByIdAsync(id, false, cancellationToken)
            ?? throw new NotFoundException("La regla de alerta solicitada no existe."));

    public async Task<AlertRuleResponse> CreateAsync(
        CreateAlertRuleRequest request,
        CancellationToken cancellationToken)
    {
        Community community = await communities.GetByIdAsync(request.CommunityId, true, cancellationToken)
            ?? throw new NotFoundException("La comunidad solicitada no existe.");

        string prefix = $"ALT-{Abbreviation(community.Name)}-{VariableAbbreviation(request.Variable)}-";
        IReadOnlyList<AlertRule> currentRules = await rules.GetAllAsync(cancellationToken);
        int sequence = currentRules.Where(item => item.CommunityId == request.CommunityId)
            .Select(item => ParseSequence(item.Code, prefix)).DefaultIfEmpty(0).Max() + 1;
        string code = $"{prefix}{sequence:000}";

        Sensor? sensor = null;
        if (request.SensorId.HasValue)
        {
            sensor = await sensors.GetByIdAsync(request.SensorId.Value, true, cancellationToken)
                ?? throw new NotFoundException("El sensor solicitado no existe.");
        }

        AlertRule rule;
        try
        {
            string message = string.IsNullOrWhiteSpace(request.Name)
                ? DefaultMessage(request.Variable, request.DangerLevel)
                : request.Name;
            rule = new AlertRule(
                community, code, message, request.Phenomenon, request.Variable,
                request.DangerLevel, request.LowerLimit, request.UpperLimit, request.ValidFrom,
                timeProvider.GetUtcNow(), request.ValidUntil, sensor, request.Condition, request.ActivationPoint);
        }
        catch (ArgumentException exception)
        {
            throw new ValidationException(exception.Message);
        }

        rules.Add(rule);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(rule);
    }

    public async Task<AlertRuleResponse> ChangeStatusAsync(
        Guid id,
        ChangeAlertRuleStatusRequest request,
        CancellationToken cancellationToken)
    {
        AlertRule rule = await rules.GetByIdAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("La regla de alerta solicitada no existe.");
        if (request.IsActive) rule.Enable(); else rule.Disable();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(rule);
    }

    private static AlertRuleResponse Map(AlertRule rule) => new(
        rule.Id, rule.CommunityId, rule.SensorId, rule.Code, rule.Name, rule.Phenomenon,
        rule.Variable, rule.DangerLevel, rule.LowerLimit, rule.UpperLimit, rule.ValidFrom,
        rule.ValidUntil, rule.IsActive, rule.CreatedAt, rule.ComparisonOperator,
        rule.ActivationPoint, ClimateAlert.Application.Features.SensorReadings.SensorReadingService.UnitFor(rule.Variable));

    private static string VariableAbbreviation(ClimateAlert.Domain.Enums.ClimateVariable variable) => variable switch
    {
        ClimateAlert.Domain.Enums.ClimateVariable.Temperature => "TEMP",
        ClimateAlert.Domain.Enums.ClimateVariable.RelativeHumidity => "HUM",
        ClimateAlert.Domain.Enums.ClimateVariable.WindSpeed => "WIND",
        ClimateAlert.Domain.Enums.ClimateVariable.RainfallLevel => "RAIN",
        ClimateAlert.Domain.Enums.ClimateVariable.RiverOrReservoirLevel => "RIVER",
        _ => "VAR"
    };

    private static string DefaultMessage(
        ClimateAlert.Domain.Enums.ClimateVariable variable,
        ClimateAlert.Domain.Enums.DangerLevel level) =>
        $"{VariableLabel(variable)} alcanzó el nivel {LevelLabel(level)}.";

    private static string VariableLabel(ClimateAlert.Domain.Enums.ClimateVariable variable) => variable switch
    {
        ClimateAlert.Domain.Enums.ClimateVariable.Temperature => "Temperatura",
        ClimateAlert.Domain.Enums.ClimateVariable.RelativeHumidity => "Humedad relativa",
        ClimateAlert.Domain.Enums.ClimateVariable.WindSpeed => "Velocidad del viento",
        ClimateAlert.Domain.Enums.ClimateVariable.RainfallLevel => "Nivel de lluvia",
        ClimateAlert.Domain.Enums.ClimateVariable.RiverOrReservoirLevel => "Nivel de río o reservorio",
        _ => "La variable monitoreada"
    };

    private static string LevelLabel(ClimateAlert.Domain.Enums.DangerLevel level) => level switch
    {
        ClimateAlert.Domain.Enums.DangerLevel.Yellow => "Preventiva",
        ClimateAlert.Domain.Enums.DangerLevel.Orange => "Alta",
        ClimateAlert.Domain.Enums.DangerLevel.Red => "Crítica",
        _ => "Normal"
    };

    private static string Abbreviation(string name)
    {
        string normalized = name.Normalize(System.Text.NormalizationForm.FormD);
        string[] words = new string(normalized.Where(character =>
            System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(words.Take(3).Select(word => char.ToUpperInvariant(word[0])));
    }

    private static int ParseSequence(string code, string prefix) =>
        code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        && int.TryParse(code[prefix.Length..], out int value) ? value : 0;
}
