using Cauce.Domain.Identity;

namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Copia desde Keycloak el estado de verificación del correo a la cuenta local, solo hacia verificado (acta A39).
/// </summary>
public interface IEmailVerificationSync
{
    /// <summary>
    /// Consulta Keycloak y, si el correo ya está verificado allí, lo marca en la cuenta local. No persiste: lo
    /// confirma el <c>SaveChangesAsync</c> del llamador. Un fallo de Keycloak se registra y no se propaga.
    /// </summary>
    /// <param name="user">Cuenta local ya autenticada.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task TrySyncAsync(User user, CancellationToken ct = default);
}
