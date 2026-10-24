using System.Security.Claims;
using System.Text.Json;
using CommunityService.Data;
using Microsoft.EntityFrameworkCore;
namespace CommunityService.Services;
public sealed class CommunityAuditEvent
{
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Payload {get;set;}="";
    public DateTimeOffset CreatedAt {get;set;}=DateTimeOffset.UtcNow;
    public DateTimeOffset? DeliveredAt {get;set;}
}
/// <summary>Retries committed domain audit events; no end-user token is stored.</summary>
public sealed class CommunityAuditWorker(IServiceScopeFactory scopes,IHttpClientFactory clients,IConfiguration config):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope=scopes.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<CommunityDbContext>();
                foreach(var item in await db.Set<CommunityAuditEvent>().Where(e=>e.DeliveredAt==null).OrderBy(e=>e.CreatedAt).Take(20).ToListAsync(ct))
                {
                    using var request=new HttpRequestMessage(HttpMethod.Post,(config["AUDIT_API_URL"]??"http://audit-service:8000")+"/internal/auth-events");
                    request.Headers.TryAddWithoutValidation("X-Internal-Service-Key",config["AUTH_INTERNAL_KEY"]);
                    request.Content=new StringContent(item.Payload,System.Text.Encoding.UTF8,"application/json");
                    using var response=await clients.CreateClient("internal").SendAsync(request,ct);
                    if(!response.IsSuccessStatusCode)break;
                    item.DeliveredAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync(ct);
                }
            }
            catch(Exception)when(!ct.IsCancellationRequested){ /* Retain durable events for retry. */ }
            await Task.Delay(1000,ct);
        }
    }
}
