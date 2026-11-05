using ClimateAlert.Domain.Entities;
namespace ClimateAlert.Domain.Tests;
public class OfficialRoleTests
{
    [Theory]
    [InlineData("ADMIN","Administrator")]
    [InlineData("OPERATOR","Operator")]
    [InlineData("VIEWER","ConsultationUser")]
    public void OfficialRolesKeepExistingAuthorizationSemantics(string official,string legacy)
        => Assert.Equal(legacy,UserRoles.Normalize(official));
    [Fact]
    public void UnknownRoleCannotGainPermissions() => Assert.Throws<ArgumentException>(()=>UserRoles.Normalize("SUPERADMIN"));
}
