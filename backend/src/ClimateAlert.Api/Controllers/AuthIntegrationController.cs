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
    public static void MapAuthIntegration(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("/internal/auth-events", Receive).ExcludeFromDescription();

    private static async Task<IResult> Receive(AuthAuditEvent item, HttpContext context,
        ClimateAlertDbContext db, IConfiguration configuration, CancellationToken cancellationToken)
    {
        if (!ExternalIdentityIntegration.HasServiceKey(context, configuration)) return Results.Unauthorized();
        string[] actions = ["Login", "Logout", "UsuarioCreado", "UsuarioEditado", "UsuarioActivado", "UsuarioDesactivado", "RolUsuarioCambiado"];
        if (item.Id == Guid.Empty || item.Actor is null || item.Target is null || !actions.Contains(item.Action)
            || string.IsNullOrWhiteSpace(item.Description) || item.Description.Length > 1000) return Results.BadRequest();
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
