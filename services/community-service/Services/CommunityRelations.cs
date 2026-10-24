using System.Text.Json;
namespace CommunityService.Services;
/// <summary>Checks external references through owner HTTP APIs, never through foreign databases.</summary>
public sealed class CommunityRelations(IHttpClientFactory clients, IConfiguration configuration, IHttpContextAccessor context)
{
    public async Task<int> Count(Guid id, bool alerts, CancellationToken ct)
    {
        string url = (configuration[alerts ? "ALERT_API_URL" : "SENSOR_API_URL"] ?? "http://localhost:5152").TrimEnd('/');
        using var response = await clients.CreateClient("internal").GetAsync(
            $"{url}/api/{(alerts ? "alerts" : "sensors")}?communityId={id}&page=1&pageSize=1", ct);
        response.EnsureSuccessStatusCode();
        int count=(await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("totalCount").GetInt32();
        if (!alerts || count>0) return count;
        var http=clients.CreateClient("internal");
        var rules=await http.GetFromJsonAsync<JsonElement>(url+"/api/alert-rules",ct);
        if(rules.EnumerateArray().Any(r=>r.GetProperty("communityId").GetGuid()==id))return 1;
        using var request=new HttpRequestMessage(HttpMethod.Get,$"{url}/api/events?communityId={id}&page=1&pageSize=1");
        request.Headers.TryAddWithoutValidation("Authorization",context.HttpContext?.Request.Headers.Authorization.ToString());
        using var events=await http.SendAsync(request,ct);events.EnsureSuccessStatusCode();
        return (await events.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("totalCount").GetInt32();
    }
}
