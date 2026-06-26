using Cauce.Domain.Identity;
using Cauce.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeder idempotente del catálogo de roles. Inserta los roles fijos
/// (<c>patient</c> y <c>nutritionist</c>) si aún no existen.
/// </summary>
public sealed class UserRolesSeeder
{
    private readonly CauceDbContext _context;
    private readonly ILogger<UserRolesSeeder> _logger;

    /// <summary>
    /// Inicializa el seeder con sus dependencias.
    /// </summary>
    /// <param name="context">Contexto de base de datos.</param>
    /// <param name="logger">Logger de la categoría del seeder.</param>
    public UserRolesSeeder(CauceDbContext context, ILogger<UserRolesSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Siembra los roles del catálogo de forma idempotente.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var roles = new (string Name, string Description)[]
        {
            (UserRoles.Patient, "Paciente con SII participante del piloto"),
            (UserRoles.Nutritionist, "Dietista-nutricionista revisor clínico")
        };

        var added = 0;
        foreach (var (name, description) in roles)
        {
            var exists = await _context.Set<UserRoleEntity>()
                .AnyAsync(x => x.RoleName == name, ct)
                .ConfigureAwait(false);

            if (!exists)
            {
                _context.Set<UserRoleEntity>().Add(new UserRoleEntity
                {
                    RoleName = name,
                    Description = description,
                    IsActive = true
                });
                added++;
            }
        }

        if (added > 0)
        {
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("Seeded {Count} user role(s).", added);
        }
    }
}
