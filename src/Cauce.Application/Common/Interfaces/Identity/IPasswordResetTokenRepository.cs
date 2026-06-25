using Cauce.Domain.Identity;

namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Repositorio del agregado <see cref="PasswordResetToken"/>.
/// </summary>
public interface IPasswordResetTokenRepository
{
    /// <summary>
    /// Busca un token de restablecimiento por su hash.
    /// </summary>
    /// <param name="tokenHash">Hash SHA-256 (hex) del token.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El token o <see langword="null"/> si no existe.</returns>
    Task<PasswordResetToken?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>
    /// Agrega un nuevo token de restablecimiento al contexto de persistencia.
    /// </summary>
    /// <param name="token">Token a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(PasswordResetToken token, CancellationToken ct = default);
}
