using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
namespace ClimateAlert.Api.Controllers;
/// <summary>Transitional composition over owner APIs; never reads their databases or exposes private event identities.</summary>
public static class RemoteDashboard
{
    public static async Task<IActionResult> Compose(ControllerBase controller,Guid? communityId,IConfiguration config,CancellationToken ct)
    {
        using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(5)};
        async Task<JsonNode> Get(string address,bool internalCall=false)
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,address);
            if(internalCall)request.Headers.TryAddWithoutValidation("X-Internal-Service-Key",config["AUTH_INTERNAL_KEY"]);
            request.Headers.TryAddWithoutValidation("X-Correlation-ID",controller.HttpContext.TraceIdentifier);
            using var response=await http.SendAsync(request,ct);response.EnsureSuccessStatusCode();
            return await JsonNode.ParseAsync(await response.Content.ReadAsStreamAsync(ct),cancellationToken:ct)??throw new HttpRequestException();
        }
        async Task<JsonArray> Pages(string address)
        {
            var output=new JsonArray();for(int page=1;page<=20;page++)
            {
                var batch=await Get(address+$"&page={page}&pageSize=100");
                foreach(var row in batch["data"]!.AsArray())output.Add(row!.DeepClone());
                if(!batch["hasNext"]!.GetValue<bool>())return output;
            }
            throw new HttpRequestException("Dashboard source limit");
        }
        try
        {
            string communitiesUrl=config["DASHBOARD_COMMUNITY_URL"]!.TrimEnd('/'),sensorUrl=config["DASHBOARD_SENSOR_URL"]!.TrimEnd('/'),alertUrl=config["DASHBOARD_ALERT_URL"]!.TrimEnd('/');
            var communities=(await Get(communitiesUrl+"/api/communities")).AsArray();
            var community=communities.FirstOrDefault(c=>!communityId.HasValue||c!["id"]!.GetValue<Guid>()==communityId.Value);
            if(community==null)return controller.NotFound(new{message="No existe una comunidad disponible."});
            var id=community["id"]!.GetValue<Guid>();
            var sensors=await Pages(sensorUrl+$"/api/sensors?communityId={id}");
            var alerts=await Pages(alertUrl+$"/api/alerts?communityId={id}&status=Open");
            var evolution=(await Get(sensorUrl+$"/internal/dashboard-readings?communityId={id}",true)).AsArray();
            var events=await Get(alertUrl+$"/internal/events/summary?communityId={id}",true);
            int active=sensors.Count(s=>s!["status"]!.GetValue<string>()=="Active");
            var indicators=new JsonArray();
            foreach(var sensor in sensors.Where(s=>s!["status"]!.GetValue<string>()=="Active"))
            {
                var sid=sensor!["id"]!.GetValue<Guid>();
                var reading=evolution.Where(r=>r!["sensorId"]!.GetValue<Guid>()==sid).OrderByDescending(r=>r!["measuredAt"]!.GetValue<DateTimeOffset>()).FirstOrDefault();
                string[] levels=["Green","Yellow","Orange","Red"];
                var level=alerts.Where(a=>a!["sensorId"]!.GetValue<Guid>()==sid).Select(a=>a!["level"]!.GetValue<string>()).OrderByDescending(l=>Array.IndexOf(levels,l)).FirstOrDefault()??"Green";
                indicators.Add(new JsonObject{["sensorId"]=sid,["variable"]=sensor["measurementType"]!.DeepClone(),["value"]=reading?["value"]?.DeepClone(),
                    ["unit"]=reading?["unit"]?.DeepClone(),["level"]=level,["measuredAt"]=reading?["measuredAt"]?.DeepClone()});
            }
            var distribution=new JsonArray();foreach(var level in new[]{"Green","Yellow","Orange","Red"})
                distribution.Add(new JsonObject{["level"]=level,["count"]=alerts.Count(a=>a!["level"]!.GetValue<string>()==level)});
            return controller.Ok(new JsonObject{["totalCommunities"]=communities.Count,["activeSensors"]=active,["inactiveSensors"]=sensors.Count-active,
                ["activeAlerts"]=alerts.Count,["alertDistributionByLevel"]=distribution,["readingEvolution"]=evolution.DeepClone(),["recentEvents"]=events.DeepClone(),
                ["communityId"]=id,["communityName"]=community["name"]!.DeepClone(),["sensors"]=sensors,["alerts"]=alerts,["indicators"]=indicators,
                ["lastUpdated"]=evolution.OrderByDescending(r=>r!["receivedAt"]!.GetValue<DateTimeOffset>()).FirstOrDefault()?["receivedAt"]?.DeepClone()});
        }
        catch(Exception e)when(e is HttpRequestException or OperationCanceledException)
        {return controller.StatusCode(503,new{detail="Dashboard sources unavailable.",correlationId=controller.HttpContext.TraceIdentifier});}
    }
}
