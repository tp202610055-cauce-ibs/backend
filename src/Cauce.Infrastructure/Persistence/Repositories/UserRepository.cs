using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using Cauce.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="IUserRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly CauceDbContext _context;
    private readonly IRoleNameCache _roleNameCache;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos y el caché de nombres de rol.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    /// <param name="roleNameCache">Caché del catálogo de roles, compartido por todo el proceso.</param>
    public UserRepository(CauceDbContext context, IRoleNameCache roleNameCache)
    {
        _context = context;
        _roleNameCache = roleNameCache;
    }

    /// <inheritdoc />
    public Task<User?> FindByIdAsync(Guid userId, CancellationToken ct = default)
    {
        return _context.Set<User>().FirstOrDefaultAsync(x => x.Id == userId, ct);
    }

    /// <inheritdoc />
    public Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        return _context.Set<User>().FirstOrDefaultAsync(x => x.Email == email, ct);
    }

    /// <inheritdoc />
    public Task<User?> FindByKeycloakIdAsync(string keycloakId, CancellationToken ct = default)
    {
        return _context.Set<User>().FirstOrDefaultAsync(x => x.KeycloakId == keycloakId, ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        await _context.Set<User>().AddAsync(user, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
    {
        return _context.Set<User>().AsNoTracking().AnyAsync(x => x.Email == email, ct);
    }

    /// <inheritdoc />
    public async Task<int> GetRoleIdAsync(string roleName, CancellationToken ct = default)
    {
        var roleId = await _context.Set<UserRoleEntity>()
            .AsNoTracking()
            .Where(x => x.RoleName == roleName)
            .Select(x => (int?)x.RoleId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return roleId ?? throw new InvalidOperationException($"El rol '{roleName}' no existe en el catálogo.");
    }

    /// <inheritdoc />
    public async Task<string> GetRoleNameAsync(int roleId, CancellationToken ct = default)
    {
        if (_roleNameCache.TryGetName(roleId, out var cached))
        {
            return cached;
        }

        // El catálogo tiene dos filas y no cambia en runtime, así que se trae entero de una vez en
        // lugar de consultar rol por rol.
        var catalog = await _context.Set<UserRoleEntity>()
            .AsNoTracking()
            .ToDictionaryAsync(x => x.RoleId, x => x.RoleName, ct)
            .ConfigureAwait(false);

        _roleNameCache.Store(catalog);

        return catalog.TryGetValue(roleId, out var roleName)
            ? roleName
            : throw new InvalidOperationException($"El rol con identificador {roleId} no existe en el catálogo.");
    }
}
