using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="IInvitationCodeRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class InvitationCodeRepository : IInvitationCodeRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public InvitationCodeRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<InvitationCode?> FindByCodeAsync(string code, CancellationToken ct = default)
    {
        return _context.Set<InvitationCode>().FirstOrDefaultAsync(x => x.Code == code, ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(InvitationCode invitation, CancellationToken ct = default)
    {
        await _context.Set<InvitationCode>().AddAsync(invitation, ct).ConfigureAwait(false);
    }
}
