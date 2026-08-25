using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Domain.Entities;

namespace ClimateAlert.Application.Features.Authentication;

public sealed class AuthenticationService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    private const string InvalidCredentialsMessage = "Correo o contraseña incorrectos.";

    public async Task<AuthenticationResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        string email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;

        User? user = await users.GetByEmailAsync(email, true, cancellationToken);

        // Mensaje genérico: nunca revelamos si el correo existe o si la contraseña falló.
        if (user is null || !passwordHasher.Verify(request.Password ?? string.Empty, user.PasswordHash))
        {
            throw new AuthenticationFailedException(InvalidCredentialsMessage);
        }

        if (!user.IsActive)
        {
            throw new AuthenticationFailedException(InvalidCredentialsMessage);
        }

        user.RegisterAccess(timeProvider.GetUtcNow());

        AuthenticationResponse response = await IssueTokensAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<AuthenticationResponse> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        string plainTextToken = request.RefreshToken?.Trim() ?? string.Empty;
        string tokenHash = tokenService.HashRefreshToken(plainTextToken);

        RefreshToken? existing = await refreshTokens.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (existing is null)
        {
            throw new AuthenticationFailedException("El token de actualización no es válido.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        if (existing.IsRevoked || existing.ExpiresAt <= now)
        {
            throw new AuthenticationFailedException("El token de actualización expiró o fue revocado.");
        }

        User? user = await users.GetByIdAsync(existing.UserId, true, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new AuthenticationFailedException("El usuario asociado ya no está disponible.");
        }

        existing.Revoke(now, "rotated");

        AuthenticationResponse response = await IssueTokensAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        string plainTextToken = request.RefreshToken?.Trim() ?? string.Empty;
        string tokenHash = tokenService.HashRefreshToken(plainTextToken);

        RefreshToken? existing = await refreshTokens.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (existing is null || existing.IsRevoked)
        {
            return;
        }

        existing.Revoke(timeProvider.GetUtcNow(), "logout");
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthenticatedUserResponse> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        User user = await users.GetByIdAsync(userId, false, cancellationToken)
            ?? throw new NotFoundException("El usuario solicitado no existe.");

        return Map(user);
    }

    private async Task<AuthenticationResponse> IssueTokensAsync(
        User user,
        CancellationToken cancellationToken)
    {
        GeneratedAccessToken accessToken = tokenService.GenerateAccessToken(user);
        GeneratedRefreshToken refreshToken = tokenService.GenerateRefreshToken();

        RefreshToken entity = new(
            user,
            refreshToken.Hash,
            timeProvider.GetUtcNow(),
            refreshToken.ExpiresAt);

        refreshTokens.Add(entity);

        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();

        return new AuthenticationResponse(
            accessToken.Token,
            accessToken.ExpiresAt,
            refreshToken.PlainText,
            refreshToken.ExpiresAt,
            Map(user));
    }

    private static AuthenticatedUserResponse Map(User user) =>
        new(user.Id, user.Name, user.Email, user.Role);
}
