namespace ClimateAlert.Domain.Entities;

public static class UserRoles
{
    public const string Administrator = "Administrator";
    public const string Operator = "Operator";
    public const string ConsultationUser = "ConsultationUser";

    // Existing persisted users and JWTs retain read-only access.
    public static string Normalize(string role) => role.Trim() switch
    {
        "User" or "Viewer" or "VIEWER" => ConsultationUser,
        "ADMIN" => Administrator,
        "OPERATOR" => Operator,
        Administrator => Administrator,
        Operator => Operator,
        ConsultationUser => ConsultationUser,
        _ => throw new ArgumentException("Unknown user role.", nameof(role))
    };
}
