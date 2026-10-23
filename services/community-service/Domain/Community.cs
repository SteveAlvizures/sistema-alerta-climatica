namespace CommunityService.Domain;

public sealed class Community
{
    private Community()
    {
    }

    public Community(
        string name,
        string location,
        string? description,
        string municipality,
        string department,
        string country,
        decimal latitude,
        decimal longitude,
        bool isActive,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();

        Update(
            name,
            location,
            description,
            municipality,
            department,
            country,
            latitude,
            longitude,
            isActive);

        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string Location { get; private set; } = null!;

    public string? Description { get; private set; }

    public string Municipality { get; private set; } = null!;

    public string Department { get; private set; } = null!;

    public string Country { get; private set; } = null!;

    public decimal Latitude { get; private set; }

    public decimal Longitude { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void Update(
        string name,
        string location,
        string? description,
        string municipality,
        string department,
        string country,
        decimal latitude,
        decimal longitude,
        bool isActive)
    {
        Name = Required(name, nameof(name), 150);
        Location = Required(location, nameof(location), 250);

        Description = NormalizeOptional(
            description,
            nameof(description),
            1000);

        Municipality = Required(
            municipality,
            nameof(municipality),
            150);

        Department = Required(
            department,
            nameof(department),
            150);

        Country = Required(
            country,
            nameof(country),
            100);

        if (latitude is < -90 or > 90)
            throw new ArgumentException(
                "La latitud debe estar entre -90 y 90.",
                nameof(latitude));

        if (longitude is < -180 or > 180)
            throw new ArgumentException(
                "La longitud debe estar entre -180 y 180.",
                nameof(longitude));

        Latitude = latitude;
        Longitude = longitude;
        IsActive = isActive;
    }

    public void ChangeStatus(bool isActive)
    {
        IsActive = isActive;
    }

    private static string Required(
        string? value,
        string parameterName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(
                "El valor es obligatorio.",
                parameterName);

        string normalized = value.Trim();

        if (normalized.Length > maxLength)
            throw new ArgumentException(
                $"El valor no puede superar {maxLength} caracteres.",
                parameterName);

        return normalized;
    }

    private static string? NormalizeOptional(
        string? value,
        string parameterName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string normalized = value.Trim();

        if (normalized.Length > maxLength)
            throw new ArgumentException(
                $"El valor no puede superar {maxLength} caracteres.",
                parameterName);

        return normalized;
    }
}
