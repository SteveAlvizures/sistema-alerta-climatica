namespace ClimateAlert.Domain.Entities;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public RefreshToken(User user, string tokenHash, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        User = user ?? throw new ArgumentNullException(nameof(user));
        UserId = user.Id;

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        }

        if (expiresAt <= issuedAt)
        {
            throw new ArgumentException("Expiration must be later than issuance.", nameof(expiresAt));
        }

        Id = Guid.NewGuid();
        TokenHash = tokenHash.Trim();
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        CreatedAt = issuedAt;

        User.AddRefreshToken(this);
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsRevoked => RevokedAt.HasValue;

    public void Revoke(DateTimeOffset revokedAt, string reason)
    {
        if (IsRevoked)
        {
            throw new InvalidOperationException("Refresh token is already revoked.");
        }

        if (revokedAt < IssuedAt)
        {
            throw new ArgumentException("Revocation cannot precede issuance.", nameof(revokedAt));
        }

        if (revokedAt > ExpiresAt)
        {
            throw new ArgumentException("Revocation cannot occur after expiration.", nameof(revokedAt));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Revocation reason is required.", nameof(reason));
        }

        RevokedAt = revokedAt;
        RevocationReason = reason.Trim();
    }
}
