namespace ClimateAlert.Application.Features.Communities;

public sealed record CreateCommunityRequest(string Name, string Location, string? Description,
    string? Municipality = null, string? Department = null, string? Country = null,
    decimal? Latitude = null, decimal? Longitude = null, bool IsActive = true);
public sealed record UpdateCommunityRequest(string Name, string Location, string? Description,
    string? Municipality = null, string? Department = null, string? Country = null,
    decimal? Latitude = null, decimal? Longitude = null, bool? IsActive = null);
public sealed record ChangeCommunityStatusRequest(bool IsActive);
public sealed record CommunityResponse(Guid Id, string Name, string Location, string? Description,
    bool IsActive, DateTimeOffset CreatedAt, string? Municipality = null, string? Department = null,
    string? Country = null, decimal? Latitude = null, decimal? Longitude = null, int SensorCount = 0);
