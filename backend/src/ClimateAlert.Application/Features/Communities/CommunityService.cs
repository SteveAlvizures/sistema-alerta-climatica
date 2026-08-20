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
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Location))
        {
            throw new ValidationException("El nombre y la ubicación son obligatorios.");
        }

        if (await communities.ExistsAsync(request.Name.Trim(), request.Location.Trim(), cancellationToken))
        {
            throw new ConflictException("Ya existe una comunidad con el mismo nombre y ubicación.");
        }

        var community = new Community(request.Name, request.Location, request.Description, timeProvider.GetUtcNow());
        communities.Add(community);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(community);
    }

    private static CommunityResponse Map(Community community) => new(
        community.Id, community.Name, community.Location, community.Description,
        community.IsActive, community.CreatedAt);
}
