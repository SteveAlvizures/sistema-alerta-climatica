using System.ComponentModel.DataAnnotations;

namespace CommunityService.Dtos;

public sealed record CreateCommunityRequest(
    [Required, MaxLength(150)]
    string Name,

    [Required, MaxLength(250)]
    string Location,

    [MaxLength(1000)]
    string? Description,

    [MaxLength(150)]
    string? Municipality,

    [MaxLength(150)]
    string? Department,

    [MaxLength(100)]
    string? Country,

    [Range(-90, 90)]
    decimal? Latitude,

    [Range(-180, 180)]
    decimal? Longitude,

    bool IsActive = true);

public sealed record UpdateCommunityRequest(
    [Required, MaxLength(150)]
    string Name,

    [Required, MaxLength(250)]
    string Location,

    [MaxLength(1000)]
    string? Description,

    [MaxLength(150)]
    string? Municipality,

    [MaxLength(150)]
    string? Department,

    [MaxLength(100)]
    string? Country,

    [Range(-90, 90)]
    decimal? Latitude,

    [Range(-180, 180)]
    decimal? Longitude,

    bool? IsActive = null);

public sealed record ChangeCommunityStatusRequest(
    bool IsActive);

public sealed record CommunityResponse(
    Guid Id,
    string Name,
    string Location,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string? Municipality,
    string? Department,
    string? Country,
    decimal? Latitude,
    decimal? Longitude,
    int SensorCount = 0);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Data,
    int PageIndex,
    int PageSize,
    int TotalPages,
    int TotalCount,
    bool HasPrevious,
    bool HasNext);
