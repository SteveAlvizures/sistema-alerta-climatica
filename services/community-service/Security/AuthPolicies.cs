namespace CommunityService.Security;

public static class AuthPolicies
{
    public const string AuthenticatedUser =
        "AuthenticatedUser";

    public const string OperateSystem =
        "OperateSystem";

    public const string ReadCommunities =
        "ReadCommunities";

    public const string WriteCommunities =
        "WriteCommunities";
}
