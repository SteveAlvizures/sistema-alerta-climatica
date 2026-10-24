using ClimateAlert.Api.Authentication;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Api.Controllers;

public sealed record AuthAuditEvent(Guid Id, ExternalIdentity Actor, ExternalIdentity Target,
    string Action, string Description, Guid AffectedRecordId, DateTimeOffset OccurredAt);

// Transitional audit ingestion only. Never exposed by Gateway, never exports passwords.
public static class AuthIntegrationEndpoints
{
    public static void MapAuthIntegration(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/auth-events", Receive).ExcludeFromDescription();
        endpoints.MapGet("/internal/audit-export", Export).ExcludeFromDescription();
    }

    // Transitional HTTP feed preserves atomic legacy audit writes without exposing SQL credentials to Audit.
    private static async Task<IResult> Export(HttpContext context, ClimateAlertDbContext db, IConfiguration configuration,
        DateTimeOffset? after, Guid? afterId, CancellationToken cancellationToken)
    {
        if (!ExternalIdentityIntegration.HasServiceKey(context, configuration)) return Results.Unauthorized();
        var query = db.AuditActions.AsNoTracking();
        if (after.HasValue)
            query = query.Where(a => a.OccurredAt > after.Value || (a.OccurredAt == after.Value &&
                (!afterId.HasValue || a.Id.CompareTo(afterId.Value) > 0)));
        return Results.Ok(await query.OrderBy(a => a.OccurredAt).ThenBy(a => a.Id).Take(100)
            .Select(a => new { a.Id, a.OccurredAt, Username = a.User.Name, a.Action, a.AffectedEntity,
                a.AffectedRecordId, a.Description, UserId = a.UserId }).ToListAsync(cancellationToken));
    }

    private static async Task<IResult> Receive(AuthAuditEvent item, HttpContext context,
        ClimateAlertDbContext db, IConfiguration configuration, CancellationToken cancellationToken)
    {
        if (!ExternalIdentityIntegration.HasServiceKey(context, configuration)) return Results.Unauthorized();
        string[] actions = ["Login", "Logout", "UsuarioCreado", "UsuarioEditado", "UsuarioActivado", "UsuarioDesactivado", "RolUsuarioCambiado"];
        if (item.Id == Guid.Empty || item.Actor is null || item.Target is null || !actions.Contains(item.Action)
            || string.IsNullOrWhiteSpace(item.Description) || item.Description.Length > 1000) return Results.BadRequest();
        // Outbox delivery is at least once: an acknowledged event ID must not create another audit row on retry.
        if (await db.AuditActions.AnyAsync(action => action.Id == item.Id, cancellationToken)) return Results.NoContent();
        var actor = await ExternalIdentityIntegration.ProjectAsync(db, item.Actor, cancellationToken);
        if (item.Target.Id != item.Actor.Id) await ExternalIdentityIntegration.ProjectAsync(db, item.Target, cancellationToken);
        var audit = new AuditAction(actor, item.Action, item.Description, "User", item.AffectedRecordId, item.OccurredAt);
        db.AuditActions.Add(audit);
        db.Entry(audit).Property(action => action.Id).CurrentValue = item.Id;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}
