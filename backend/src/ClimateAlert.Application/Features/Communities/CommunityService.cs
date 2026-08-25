using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;

namespace ClimateAlert.Application.Features.Communities;

public sealed class CommunityService(
    ICommunityRepository communities,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
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

    private static void Validate(string name, string location, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(location))
            throw new ValidationException("El nombre y la ubicación son obligatorios.");
        if (name.Trim().Length > 150 || location.Trim().Length > 250 || description?.Trim().Length > 1000)
            throw new ValidationException("Uno o más campos exceden la longitud permitida.");
    }

    private static CommunityResponse Map(Community community) => new(
        community.Id, community.Name, community.Location, community.Description,
        community.IsActive, community.CreatedAt);
}
