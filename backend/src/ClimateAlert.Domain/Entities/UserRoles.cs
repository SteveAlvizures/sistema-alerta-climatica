namespace ClimateAlert.Domain.Entities;

public static class UserRoles
{
    public const string Administrator = "Administrator";
    public const string Operator = "Operator";
    public const string Query = "Query";

    public static readonly Guid AdministratorId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid OperatorId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static readonly Guid QueryId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static string Validate(string role)
    {
        string value = role?.Trim()
            ?? throw new ArgumentNullException(nameof(role));

        return value switch
        {
            Administrator => Administrator,
            Operator => Operator,
            Query => Query,
            _ => throw new ArgumentException(
                "The role must be Administrator, Operator or Query.",
                nameof(role))
        };
    }
}
