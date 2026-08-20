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

        string code = request.Code?.Trim() ?? string.Empty;
        if (await rules.ExistsAsync(request.CommunityId, code, cancellationToken))
        {
            throw new ConflictException("Ya existe una regla con el mismo código en la comunidad.");
        }

        Sensor? sensor = null;
        if (request.SensorId.HasValue)
        {
            sensor = await sensors.GetByIdAsync(request.SensorId.Value, true, cancellationToken)
                ?? throw new NotFoundException("El sensor solicitado no existe.");
        }

        AlertRule rule;
        try
        {
            rule = new AlertRule(
                community, code, request.Name, request.Phenomenon, request.Variable,
                request.DangerLevel, request.LowerLimit, request.UpperLimit, request.ValidFrom,
                timeProvider.GetUtcNow(), request.ValidUntil, sensor);
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
        rule.ValidUntil, rule.IsActive, rule.CreatedAt);
}
