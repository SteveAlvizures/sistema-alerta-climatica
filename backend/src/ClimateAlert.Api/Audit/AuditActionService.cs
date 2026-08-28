using System.Security.Claims;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Features.SensorReadings;
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

    public async Task<PagedResponse<AuditActionResponse>> GetPageAsync(
        int page,
        int pageSize,
        string? username,
        string? action,
        string? entity,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        CancellationToken cancellationToken)
    {
        if (page < 1) throw new ValidationException("La página debe ser mayor o igual a 1.");
        if (pageSize < 1 || pageSize > 100) throw new ValidationException("El tamaño de página debe estar entre 1 y 100.");
        if (dateFrom.HasValue && dateTo.HasValue && dateFrom > dateTo)
            throw new ValidationException("La fecha desde no puede ser posterior a la fecha hasta.");

        IQueryable<AuditAction> query = database.AuditActions.AsNoTracking().Include(item => item.User);

        if (!string.IsNullOrWhiteSpace(username))
        {
            string text = username.Trim();
            query = query.Where(item => item.User.Name.Contains(text));
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            string normalizedAction = action.Trim();
            query = query.Where(item => item.Action == normalizedAction);
        }

        if (!string.IsNullOrWhiteSpace(entity))
        {
            string text = entity.Trim();
            query = query.Where(item => item.AffectedEntity.Contains(text));
        }

        if (dateFrom.HasValue) query = query.Where(item => item.OccurredAt >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(item => item.OccurredAt <= dateTo.Value);

        int totalCount = await query.CountAsync(cancellationToken);
        IReadOnlyList<AuditActionResponse> data = await query.OrderByDescending(item => item.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AuditActionResponse(
                item.Id, item.OccurredAt, item.User.Name, item.Action, item.AffectedEntity,
                item.AffectedRecordId, item.Description))
            .ToListAsync(cancellationToken);
        int totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new(data, page, pageSize, totalPages, totalCount, page > 1, page < totalPages);
    }
}
