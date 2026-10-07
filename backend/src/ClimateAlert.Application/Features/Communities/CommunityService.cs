using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;

namespace ClimateAlert.Application.Features.Communities;

public sealed class CommunityService(
    ICommunityRepository communities,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<ClimateAlert.Application.Features.SensorReadings.PagedResponse<CommunityResponse>> GetPageAsync(
        string? search, bool? isActive, string? municipality, string? department, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ValidationException("La pagina debe ser positiva y el tamano debe estar entre 1 y 100.");
        var result = await communities.GetPageAsync(search, isActive, municipality, department, page, pageSize, cancellationToken);
        int pages = (int)Math.Ceiling(result.TotalCount / (double)pageSize);
        return new(result.Items.Select(Map).ToList(), page, pageSize, pages, result.TotalCount, page > 1, page < pages);
    }

    public async Task<CommunityResponse> ChangeStatusAsync(Guid id, ChangeCommunityStatusRequest request, CancellationToken cancellationToken)
    {
        var community = await communities.GetByIdAsync(id, true, cancellationToken) ?? throw new NotFoundException("La comunidad solicitada no existe.");
        community.ChangeStatus(request.IsActive); await unitOfWork.SaveChangesAsync(cancellationToken); return Map(community);
    }

    public async Task<IReadOnlyList<CommunityResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        (await communities.GetAllAsync(cancellationToken)).Select(Map).ToList();

    public async Task<CommunityResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Community community = await communities.GetByIdAsync(id, false, cancellationToken)
            ?? throw new NotFoundException("La comunidad solicitada no existe.");
        return Map(community);
    }

    public async Task<CommunityResponse> CreateAsync(CreateCommunityRequest request, CancellationToken cancellationToken)
    {
        Validate(request.Name, request.Location, request.Description);
        if (await communities.ExistsAsync(request.Name.Trim(), request.Location.Trim(), null, cancellationToken))
        {
            throw new ConflictException("Ya existe una comunidad con el mismo nombre y ubicación.");
        }

        var community = new Community(request.Name, request.Location, request.Description, timeProvider.GetUtcNow());
        ApplyDetails(community, request.Municipality, request.Department, request.Country, request.Latitude, request.Longitude, request.IsActive);
        communities.Add(community);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(community);
    }

    public async Task<CommunityResponse> UpdateAsync(Guid id, UpdateCommunityRequest request, CancellationToken cancellationToken)
    {
        Validate(request.Name, request.Location, request.Description);
        Community community = await communities.GetByIdAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("La comunidad solicitada no existe.");
        if (await communities.ExistsAsync(request.Name.Trim(), request.Location.Trim(), id, cancellationToken))
        {
            throw new ConflictException("Ya existe una comunidad con el mismo nombre y ubicación.");
        }
        ApplyDetails(community, request.Municipality, request.Department, request.Country, request.Latitude, request.Longitude, request.IsActive ?? community.IsActive);
        community.Update(request.Name, request.Location, request.Description);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(community);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        Community community = await communities.GetByIdAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("La comunidad solicitada no existe.");
        if (await communities.HasDependenciesAsync(id, cancellationToken))
        {
            throw new ConflictException("No se puede eliminar la comunidad porque tiene sensores u otros registros asociados.");
        }
        communities.Remove(community);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyDetails(Community community, string? municipality, string? department, string? country, decimal? latitude, decimal? longitude, bool active)
    {
        if (string.IsNullOrWhiteSpace(municipality) || string.IsNullOrWhiteSpace(department) || string.IsNullOrWhiteSpace(country) || !latitude.HasValue || !longitude.HasValue)
            throw new ValidationException("Municipio, departamento, pais, latitud y longitud son obligatorios.");
        if (municipality.Trim().Length > 150 || department.Trim().Length > 150 || country.Trim().Length > 100)
            throw new ValidationException("Los campos administrativos exceden la longitud permitida.");
        try { community.SetAdministrativeDetails(municipality, department, country, latitude.Value, longitude.Value, active); }
        catch (ArgumentException e) { throw new ValidationException(e.Message); }
    }

    private static void Validate(string name, string location, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(location))
            throw new ValidationException("El nombre y la ubicación son obligatorios.");
        if (name.Trim().Length > 150 || location.Trim().Length > 250 || description?.Trim().Length > 1000)
            throw new ValidationException("Uno o más campos exceden la longitud permitida.");
    }

    private static CommunityResponse Map(Community community) => new(
        community.Id, community.Name, community.Location, community.Description,
        community.IsActive, community.CreatedAt, community.Municipality, community.Department, community.Country, community.Latitude, community.Longitude, community.Sensors.Count);
}
