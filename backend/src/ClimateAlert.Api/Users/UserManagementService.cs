using System.Security.Claims;
using ClimateAlert.Application.Common.Exceptions;
using ClimateAlert.Application.Features.SensorReadings;
using ClimateAlert.Domain.Entities;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ClimateAlert.Api.Users;

public sealed record UserResponse(
    Guid Id,
    string Name,
    string Username,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastAccessAt);

public sealed record CreateUserRequest(
    string Name,
    string Username,
    string Password,
    string Role);

public sealed record UpdateUserRequest(
    string Name,
    string Username);

public sealed record ChangeUserStatusRequest(
    [System.ComponentModel.DataAnnotations.Required] bool? IsActive);

public sealed record ChangeUserRoleRequest(string Role);

public sealed class UserManagementService(
    ClimateAlertDbContext database,
    IPasswordHasher<User> passwordHasher,
    TimeProvider timeProvider)
{
    public async Task<PagedResponse<UserResponse>> GetPageAsync(
        int page,
        int pageSize,
        string? search,
        string? role,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            throw new ValidationException(
                "La página debe ser positiva y el tamaño estar entre 1 y 100.");
        }

        var query = database.Users
            .AsNoTracking()
            .Include(user => user.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string text = search.Trim().ToLowerInvariant();

            if (text.Length > 254)
            {
                throw new ValidationException(
                    "La búsqueda no puede superar 254 caracteres.");
            }

            query = query.Where(user =>
                user.Name.ToLower().Contains(text) ||
                user.Email.ToLower().Contains(text));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            string validRole = ValidRole(role);

            query = query.Where(user =>
                user.Role.Name == validRole);
        }

        if (isActive.HasValue)
        {
            query = query.Where(user =>
                user.IsActive == isActive.Value);
        }

        int count = await query.CountAsync(cancellationToken);

        var data = await query
            .OrderBy(user => user.Name)
            .ThenBy(user => user.Id)
            .Skip((int)Math.Min(
                (long)(page - 1) * pageSize,
                int.MaxValue))
            .Take(pageSize)
            .Select(user => new UserResponse(
                user.Id,
                user.Name,
                user.Email,
                user.Role.Name,
                user.IsActive,
                user.CreatedAt,
                user.LastAccessAt))
            .ToListAsync(cancellationToken);

        int pages = (int)Math.Ceiling(count / (double)pageSize);

        return new(
            data,
            page,
            pageSize,
            pages,
            count,
            page > 1,
            page < pages);
    }

    public async Task<UserResponse> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Response(
            await FindAsync(id, cancellationToken));
    }

    public async Task<UserResponse> CreateAsync(
        CreateUserRequest request,
        ClaimsPrincipal actor,
        CancellationToken cancellationToken)
    {
        string name = ValidName(request.Name);
        string username = ValidUsername(request.Username);
        string roleName = ValidRole(request.Role);

        if (request.Password is null ||
            request.Password.Length is < 8 or > 128 ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationException(
                "La contraseña debe tener entre 8 y 128 caracteres y no estar vacía.");
        }

        await EnsureUniqueAsync(
            username,
            null,
            cancellationToken);

        Role role = await GetRoleAsync(
            roleName,
            cancellationToken);

        var user = new User(
            name,
            username,
            "pending",
            role,
            timeProvider.GetUtcNow());

        user.UpdateIdentity(
            name,
            username,
            passwordHasher.HashPassword(
                user,
                request.Password),
            role);

        database.Users.Add(user);

        await AddAuditAsync(
            actor,
            "UsuarioCreado",
            user,
            "Se creó una cuenta de usuario.",
            cancellationToken);

        await SaveAsync(cancellationToken);

        return Response(user);
    }

    public async Task<UserResponse> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        ClaimsPrincipal actor,
        CancellationToken cancellationToken)
    {
        string name = ValidName(request.Name);
        string username = ValidUsername(request.Username);

        var user = await FindAsync(
            id,
            cancellationToken);

        await EnsureUniqueAsync(
            username,
            id,
            cancellationToken);

        user.UpdateProfile(
            name,
            username);

        await AddAuditAsync(
            actor,
            "UsuarioEditado",
            user,
            "Se actualizó el nombre o login de la cuenta.",
            cancellationToken);

        await SaveAsync(cancellationToken);

        return Response(user);
    }

    public async Task<UserResponse> ChangeStatusAsync(
        Guid id,
        bool isActive,
        ClaimsPrincipal actor,
        CancellationToken cancellationToken)
    {
        var user = await FindAsync(
            id,
            cancellationToken);

        if (!isActive && id == ActorId(actor))
        {
            throw new ConflictException(
                "No puedes desactivar tu propia cuenta administrativa.");
        }

        await EnsureAdministratorRemainsAsync(
            user,
            isActive,
            user.Role.Name,
            cancellationToken);

        if (user.IsActive == isActive)
        {
            return Response(user);
        }

        user.ChangeStatus(isActive);

        await AddAuditAsync(
            actor,
            isActive
                ? "UsuarioActivado"
                : "UsuarioDesactivado",
            user,
            isActive
                ? "Se activó la cuenta."
                : "Se desactivó la cuenta.",
            cancellationToken);

        await SaveAsync(cancellationToken);

        return Response(user);
    }

    public async Task<UserResponse> ChangeRoleAsync(
        Guid id,
        string role,
        ClaimsPrincipal actor,
        CancellationToken cancellationToken)
    {
        string validRole = ValidRole(role);

        var user = await FindAsync(
            id,
            cancellationToken);

        if (validRole != UserRoles.Administrator &&
            id == ActorId(actor))
        {
            throw new ConflictException(
                "No puedes quitar el rol Administrator de tu propia cuenta.");
        }

        await EnsureAdministratorRemainsAsync(
            user,
            user.IsActive,
            validRole,
            cancellationToken);

        if (user.Role.Name == validRole)
        {
            return Response(user);
        }

        Role newRole = await GetRoleAsync(
            validRole,
            cancellationToken);

        user.ChangeRole(newRole);

        await AddAuditAsync(
            actor,
            "RolUsuarioCambiado",
            user,
            $"Se asignó el rol {validRole}.",
            cancellationToken);

        await SaveAsync(cancellationToken);

        return Response(user);
    }

    private async Task EnsureAdministratorRemainsAsync(
        User user,
        bool isActive,
        string role,
        CancellationToken cancellationToken)
    {
        if (user.IsActive &&
            user.Role.Name == UserRoles.Administrator &&
            (!isActive ||
             role != UserRoles.Administrator))
        {
            bool anotherAdministratorExists =
                await database.Users
                    .Include(other => other.Role)
                    .AnyAsync(
                        other =>
                            other.Id != user.Id &&
                            other.IsActive &&
                            other.Role.Name ==
                                UserRoles.Administrator,
                        cancellationToken);

            if (!anotherAdministratorExists)
            {
                throw new ConflictException(
                    "Debe permanecer al menos un administrador activo.");
            }
        }
    }

    private async Task<User> FindAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await database.Users
            .Include(user => user.Role)
            .SingleOrDefaultAsync(
                user => user.Id == id,
                cancellationToken)
            ?? throw new NotFoundException(
                "No se encontró el usuario.");
    }

    private async Task<Role> GetRoleAsync(
        string roleName,
        CancellationToken cancellationToken)
    {
        Role? role = await database.Roles
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Name == roleName,
                cancellationToken);

        return role
            ?? throw new NotFoundException(
                $"No se encontró el rol {roleName}.");
    }

    private async Task EnsureUniqueAsync(
        string username,
        Guid? exceptId,
        CancellationToken cancellationToken)
    {
        if (await database.Users.AnyAsync(
            user =>
                user.Id != exceptId &&
                user.Email.ToLower() == username,
            cancellationToken))
        {
            throw new ConflictException(
                "El login ya está registrado.");
        }
    }

    private async Task AddAuditAsync(
        ClaimsPrincipal actor,
        string action,
        User target,
        string description,
        CancellationToken cancellationToken)
    {
        var author = await FindAsync(
            ActorId(actor),
            cancellationToken);

        database.AuditActions.Add(
            new AuditAction(
                author,
                action,
                description,
                "User",
                target.Id,
                timeProvider.GetUtcNow()));
    }

    private async Task SaveAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await database.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException
                   { Number: 2601 or 2627 })
        {
            throw new ConflictException(
                "El login ya está registrado.");
        }
    }

    private static Guid ActorId(
        ClaimsPrincipal actor)
    {
        return Guid.TryParse(
            actor.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ?? actor.FindFirstValue("sub"),
            out var id)
            ? id
            : throw new InvalidOperationException(
                "No fue posible identificar al administrador.");
    }

    private static UserResponse Response(User user)
    {
        return new UserResponse(
            user.Id,
            user.Name,
            user.Email,
            user.Role.Name,
            user.IsActive,
            user.CreatedAt,
            user.LastAccessAt);
    }

    private static string ValidName(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) &&
               name.Trim().Length <= 150
            ? name.Trim()
            : throw new ValidationException(
                "El nombre es obligatorio y admite hasta 150 caracteres.");
    }

    private static string ValidUsername(
        string? username)
    {
        string value =
            username?.Trim().ToLowerInvariant()
            ?? "";

        if (value.Length is < 1 or > 254 ||
            value.Any(char.IsWhiteSpace) ||
            value.Any(char.IsControl))
        {
            throw new ValidationException(
                "El login es obligatorio, sin espacios y admite hasta 254 caracteres.");
        }

        return value;
    }

    private static string ValidRole(string? role)
    {
        return role is
            UserRoles.Administrator or
            UserRoles.Operator or
            UserRoles.Query
            ? role
            : throw new ValidationException(
                "Selecciona un rol válido: Administrator, Operator o Query.");
    }
}
