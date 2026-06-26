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

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public UserRepository(CauceDbContext context)
    {
        _context = context;
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
}
