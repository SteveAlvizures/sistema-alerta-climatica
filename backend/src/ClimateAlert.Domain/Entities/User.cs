namespace ClimateAlert.Domain.Entities;

public sealed class User
{
    private readonly List<RefreshToken> _refreshTokens = [];
    private readonly List<AuditAction> _auditActions = [];

    private User()
    {
    }

    public User(string name, string email, string passwordHash, string role, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        Name = Required(name, nameof(name));
        Email = Required(email, nameof(email));
        PasswordHash = Required(passwordHash, nameof(passwordHash));
        Role = Required(role, nameof(role));
        CreatedAt = createdAt;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string Role { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastAccessAt { get; private set; }
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();
    public IReadOnlyCollection<AuditAction> AuditActions => _auditActions.AsReadOnly();

    public void RegisterAccess(DateTimeOffset occurredAt) => LastAccessAt = occurredAt;

    public void UpdateIdentity(string name, string username, string passwordHash, string role)
    {
        Name = Required(name, nameof(name));
        Email = Required(username, nameof(username)).ToLowerInvariant();
        PasswordHash = Required(passwordHash, nameof(passwordHash));
        Role = Required(role, nameof(role));
    }

    internal void AddRefreshToken(RefreshToken refreshToken) => _refreshTokens.Add(refreshToken);

    internal void AddAuditAction(AuditAction auditAction) => _auditActions.Add(auditAction);

    private static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return value.Trim();
    }
}
