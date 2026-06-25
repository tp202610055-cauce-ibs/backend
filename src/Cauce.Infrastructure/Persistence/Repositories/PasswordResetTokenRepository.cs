using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="IPasswordResetTokenRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public PasswordResetTokenRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<PasswordResetToken?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        return _context.Set<PasswordResetToken>().FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(PasswordResetToken token, CancellationToken ct = default)
    {
        await _context.Set<PasswordResetToken>().AddAsync(token, ct).ConfigureAwait(false);
    }
}
