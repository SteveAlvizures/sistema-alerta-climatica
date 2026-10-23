using CommunityService.Data;
using CommunityService.Domain;
using CommunityService.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CommunityService.Services;

public sealed class CommunityManager(
    CommunityDbContext database,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<CommunityResponse>>
        GetAllAsync(
            CancellationToken cancellationToken)
    {
        var communities =
            await database.Communities
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

        return communities
            .Select(Map)
            .ToList();
    }

    public async Task<PagedResponse<CommunityResponse>>
        GetPageAsync(
            string? search,
            bool? isActive,
            string? municipality,
            string? department,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
    {
        if (page < 1 ||
            pageSize is < 1 or > 100)
        {
            throw new ArgumentException(
                "La pagina debe ser positiva y el tamano debe estar entre 1 y 100.");
        }

        IQueryable<Community> query =
            database.Communities
                .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string text =
                search.Trim().ToLower();

            query = query.Where(x =>
                x.Name.ToLower().Contains(text) ||
                x.Location.ToLower().Contains(text));
        }

        if (isActive.HasValue)
            query = query.Where(
                x => x.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(municipality))
        {
            string value =
                municipality.Trim().ToLower();

            query = query.Where(
                x => x.Municipality.ToLower() == value);
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            string value =
                department.Trim().ToLower();

            query = query.Where(
                x => x.Department.ToLower() == value);
        }

        int totalCount =
            await query.CountAsync(
                cancellationToken);

        var items =
            await query
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(
                    cancellationToken);

        int totalPages =
            (int)Math.Ceiling(
                totalCount /
                (double)pageSize);

        return new(
            items.Select(Map).ToList(),
            page,
            pageSize,
            totalPages,
            totalCount,
            page > 1,
            page < totalPages);
    }

    public async Task<CommunityResponse>
        GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
    {
        Community? community =
            await database.Communities
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

        if (community is null)
            throw new KeyNotFoundException(
                "La comunidad solicitada no existe.");

        return Map(community);
    }

    public async Task<CommunityResponse>
        CreateAsync(
            CreateCommunityRequest request,
            CancellationToken cancellationToken)
    {
        await EnsureUniqueAsync(
            request.Name,
            request.Location,
            null,
            cancellationToken);

        Community community;

        try
        {
            community = new Community(
                request.Name,
                request.Location,
                request.Description,
                request.Municipality,
                request.Department,
                request.Country,
                request.Latitude,
                request.Longitude,
                request.IsActive,
                timeProvider.GetUtcNow());
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(
                exception.Message);
        }

        database.Communities.Add(
            community);

        await database.SaveChangesAsync(
            cancellationToken);

        return Map(community);
    }

    public async Task<CommunityResponse>
        UpdateAsync(
            Guid id,
            UpdateCommunityRequest request,
            CancellationToken cancellationToken)
    {
        Community? community =
            await database.Communities
                .SingleOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

        if (community is null)
            throw new KeyNotFoundException(
                "La comunidad solicitada no existe.");

        await EnsureUniqueAsync(
            request.Name,
            request.Location,
            id,
            cancellationToken);

        community.Update(
            request.Name,
            request.Location,
            request.Description,
            request.Municipality,
            request.Department,
            request.Country,
            request.Latitude,
            request.Longitude,
            request.IsActive ??
            community.IsActive);

        await database.SaveChangesAsync(
            cancellationToken);

        return Map(community);
    }

    public async Task<CommunityResponse>
        ChangeStatusAsync(
            Guid id,
            bool isActive,
            CancellationToken cancellationToken)
    {
        Community? community =
            await database.Communities
                .SingleOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

        if (community is null)
            throw new KeyNotFoundException(
                "La comunidad solicitada no existe.");

        community.ChangeStatus(
            isActive);

        await database.SaveChangesAsync(
            cancellationToken);

        return Map(community);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        Community? community =
            await database.Communities
                .SingleOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

        if (community is null)
            throw new KeyNotFoundException(
                "La comunidad solicitada no existe.");

        /*
         * IMPORTANTE:
         * En Fase 3 no se consulta la base del Sensor Service.
         * La verificacion de sensores asociados se agregara
         * mediante API cuando Sensor Service este disponible.
         */

        database.Communities.Remove(
            community);

        await database.SaveChangesAsync(
            cancellationToken);
    }

    private async Task EnsureUniqueAsync(
        string name,
        string location,
        Guid? exceptId,
        CancellationToken cancellationToken)
    {
        string normalizedName =
            name.Trim().ToLower();

        string normalizedLocation =
            location.Trim().ToLower();

        bool exists =
            await database.Communities
                .AnyAsync(
                    x =>
                        x.Id != exceptId &&
                        x.Name.ToLower() ==
                            normalizedName &&
                        x.Location.ToLower() ==
                            normalizedLocation,
                    cancellationToken);

        if (exists)
            throw new InvalidOperationException(
                "Ya existe una comunidad con el mismo nombre y ubicación.");
    }

    private static CommunityResponse Map(
        Community community)
    {
        return new(
            community.Id,
            community.Name,
            community.Location,
            community.Description,
            community.IsActive,
            community.CreatedAt,
            community.Municipality,
            community.Department,
            community.Country,
            community.Latitude,
            community.Longitude,
            0);
    }
}
