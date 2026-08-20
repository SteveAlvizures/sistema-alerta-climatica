namespace ClimateAlert.Application.Features.Communities;

public sealed record CreateCommunityRequest(string Name, string Location, string? Description);

public sealed record CommunityResponse(
    Guid Id,
    string Name,
    string Location,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt);
