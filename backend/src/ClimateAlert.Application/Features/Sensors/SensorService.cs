using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;

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

        string code = request.Code?.Trim() ?? string.Empty;
        if (await sensors.ExistsAsync(request.CommunityId, code, cancellationToken))
        {
            throw new ConflictException("Ya existe un sensor con el mismo código en la comunidad.");
        }

        Sensor sensor;
        try
        {
            sensor = new Sensor(community, code, request.Name, request.MeasurementType,
                request.Origin, request.Location, timeProvider.GetUtcNow(), request.DeviceCode);
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

    private static SensorResponse Map(Sensor sensor) => new(
        sensor.Id, sensor.CommunityId, sensor.Code, sensor.Name, sensor.MeasurementType,
        sensor.Origin, sensor.Status, sensor.Location, sensor.DeviceCode,
        sensor.LastCommunicationAt, sensor.CreatedAt);
}
