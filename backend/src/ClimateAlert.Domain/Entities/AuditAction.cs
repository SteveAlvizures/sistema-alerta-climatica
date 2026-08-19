namespace ClimateAlert.Domain.Entities;

public sealed class AuditAction
{
    private AuditAction()
    {
    }

    public AuditAction(
        User user,
        string action,
        string description,
        string affectedEntity,
        Guid? affectedRecordId,
        DateTimeOffset occurredAt)
    {
        User = user ?? throw new ArgumentNullException(nameof(user));
        UserId = user.Id;
        Action = Required(action, nameof(action));
        Description = Required(description, nameof(description));
        AffectedEntity = Required(affectedEntity, nameof(affectedEntity));
        AffectedRecordId = affectedRecordId;
        OccurredAt = occurredAt;
        CreatedAt = occurredAt;
        Id = Guid.NewGuid();

        User.AddAuditAction(this);
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public string Action { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string AffectedEntity { get; private set; } = null!;
    public Guid? AffectedRecordId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return value.Trim();
    }
}
