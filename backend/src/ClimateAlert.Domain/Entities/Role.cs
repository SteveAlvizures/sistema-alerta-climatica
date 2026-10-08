namespace ClimateAlert.Domain.Entities;

public sealed class Role
{
    private Role()
    {
    }

    public Role(Guid id, string name, string description)
    {
        Id = id;
        Name = UserRoles.Validate(name);
        Description = Required(description, nameof(description));
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;

    private static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A value is required.", parameterName);

        return value.Trim();
    }
}
