using System.Security.Claims;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Api.Audit;

public sealed record AuditActionResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Username,
    string Action,
    string AffectedEntity,
    Guid? AffectedRecordId,
    string Description);

public sealed class AuditActionService(ClimateAlertDbContext database, TimeProvider timeProvider)
{
    public async Task RecordAsync(
        ClaimsPrincipal principal,
        string action,
        string affectedEntity,
        Guid? affectedRecordId,
        string description,
        CancellationToken cancellationToken)
    {
        string? subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out Guid userId))
        {
            throw new InvalidOperationException("No fue posible identificar al usuario autenticado.");
        }

        User user = await database.Users.SingleAsync(candidate => candidate.Id == userId, cancellationToken);
        database.AuditActions.Add(new AuditAction(
            user, action, description, affectedEntity, affectedRecordId, timeProvider.GetUtcNow()));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditActionResponse>> GetRecentAsync(
        int limit,
        string? action,
        string? search,
        CancellationToken cancellationToken)
    {
        int safeLimit = Math.Clamp(limit, 1, 200);
        IQueryable<AuditAction> query = database.AuditActions.AsNoTracking().Include(item => item.User);

        if (!string.IsNullOrWhiteSpace(action))
        {
            string normalizedAction = action.Trim();
            query = query.Where(item => item.Action == normalizedAction);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string text = search.Trim();
            query = query.Where(item => item.Description.Contains(text)
                || item.AffectedEntity.Contains(text)
                || item.User.Name.Contains(text));
        }

        return await query.OrderByDescending(item => item.OccurredAt)
            .Take(safeLimit)
            .Select(item => new AuditActionResponse(
                item.Id, item.OccurredAt, item.User.Name, item.Action, item.AffectedEntity,
                item.AffectedRecordId, item.Description))
            .ToListAsync(cancellationToken);
    }
}
