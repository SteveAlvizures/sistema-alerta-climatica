using CommunityService.Domain;
namespace CommunityValidation.Tests;
public class CommunityTests
{
    [Fact] public void LegacyMissingGeographyRemainsUnknown()
    {
        var item = new Community("Old", "Area", null, null, null, null, null, null, true, DateTimeOffset.UtcNow);
        Assert.Null(item.Latitude); Assert.Null(item.Municipality); Assert.True(item.IsActive);
    }
    [Theory][InlineData(-91, 0)][InlineData(91, 0)][InlineData(0, -181)][InlineData(0, 181)]
    public void RejectsInvalidCoordinates(decimal latitude, decimal longitude) => Assert.Throws<ArgumentException>(() =>
        new Community("Community", "Area", null, "Town", "Region", "Country", latitude, longitude, true, DateTimeOffset.UtcNow));
    [Theory][InlineData("", "Area")][InlineData("Name", " ")]
    public void RejectsMissingIdentity(string name, string location) => Assert.Throws<ArgumentException>(() =>
        new Community(name, location, null, null, null, null, null, null, true, DateTimeOffset.UtcNow));
    [Fact] public void UpdatesStateWithoutChangingIdentity()
    {
        var item = new Community(" A ", "Area", null, null, null, null, null, null, true, DateTimeOffset.UtcNow);
        var id = item.Id; item.ChangeStatus(false); Assert.Equal(id, item.Id); Assert.False(item.IsActive); Assert.Equal("A", item.Name);
    }
}
