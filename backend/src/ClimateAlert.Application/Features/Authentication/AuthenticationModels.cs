namespace ClimateAlert.Application.Features.Authentication;

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record AuthenticatedUserResponse(
    Guid Id,
    string Name,
    string Email,
    string Role);

public sealed record AuthenticationResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthenticatedUserResponse User);
