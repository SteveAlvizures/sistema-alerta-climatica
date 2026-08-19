using ClimateAlert.Domain.Entities;

namespace ClimateAlert.Domain.Tests;

public sealed class RefreshTokenTests
{
    [Fact]
    public void RevokeStoresTimeAndReason()
    {
        DateTimeOffset issuedAt = DateTimeOffset.UtcNow;
        User user = new("Community operator", "operator@example.test", "protected-hash", "Operator", issuedAt);
        RefreshToken token = new(user, "protected-token-hash", issuedAt, issuedAt.AddDays(1));
        DateTimeOffset revokedAt = issuedAt.AddHours(1);

        token.Revoke(revokedAt, "Session ended");

        Assert.True(token.IsRevoked);
        Assert.Equal(revokedAt, token.RevokedAt);
        Assert.Equal("Session ended", token.RevocationReason);
    }

    [Fact]
    public void RevokeRejectsSecondRevocation()
    {
        DateTimeOffset issuedAt = DateTimeOffset.UtcNow;
        User user = new("Community operator", "operator@example.test", "protected-hash", "Operator", issuedAt);
        RefreshToken token = new(user, "protected-token-hash", issuedAt, issuedAt.AddDays(1));
        token.Revoke(issuedAt.AddHours(1), "Session ended");

        Action revokeAgain = () => token.Revoke(issuedAt.AddHours(2), "Second request");

        Assert.Throws<InvalidOperationException>(revokeAgain);
    }
}
