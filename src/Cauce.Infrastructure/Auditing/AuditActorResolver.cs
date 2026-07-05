using Cauce.Application.Common.Interfaces;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Auditing;

/// <summary>
/// Resuelve la clave primaria local (<c>users.user_id</c>) del actor autenticado a partir de su
/// identificador de Keycloak (claim <c>sub</c>), memorizando el resultado durante la petición
/// para no repetir la consulta (acta A1). Devuelve <see langword="null"/> cuando no hay usuario
/// autenticado (por ejemplo, un worker en segundo plano) o cuando el sujeto no tiene cuenta local.
/// </summary>
public sealed class AuditActorResolver
{
    private readonly ICurrentUserService _currentUserService;
    private readonly CauceDbContext _context;
    private bool _resolved;
    private Guid? _localUserId;

    /// <summary>
    /// Inicializa el resolver con el servicio del usuario actual y el contexto de base de datos.
    /// </summary>
    /// <param name="currentUserService">Servicio del usuario autenticado actual.</param>
    /// <param name="context">Contexto de base de datos.</param>
    public AuditActorResolver(ICurrentUserService currentUserService, CauceDbContext context)
    {
        _currentUserService = currentUserService;
        _context = context;
    }

    /// <summary>
    /// Resuelve la clave primaria local del actor autenticado, o <see langword="null"/> si no aplica.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El identificador local del actor, o <see langword="null"/>.</returns>
    public async Task<Guid?> ResolveLocalActorIdAsync(CancellationToken ct = default)
    {
        if (_resolved)
        {
            return _localUserId;
        }

        var keycloakId = _currentUserService.UserId;
        if (keycloakId is not null)
        {
            var keycloakSubject = keycloakId.Value.ToString();
            _localUserId = await _context.Users
                .AsNoTracking()
                .Where(user => user.KeycloakId == keycloakSubject)
                .Select(user => (Guid?)user.Id)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }

        _resolved = true;
        return _localUserId;
    }
}
