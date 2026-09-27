using Cauce.Application.Common.Identity;

namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Contexto, con alcance de petición, de un intento de iniciar o cerrar sesión. Los handlers de
/// identidad registran aquí la causa interna de un rechazo, y el middleware de auditoría la lee al
/// escribir la fila de <c>audit_logs</c> (acta A68). Existe porque el middleware ve solo el código HTTP
/// y la excepción, y una misma respuesta genérica puede tener causas muy distintas.
/// </summary>
public interface IAuthenticationAttemptContext
{
    /// <summary>
    /// Causa interna del rechazo (una de las constantes de <see cref="AuthFailureCauses"/>), o
    /// <see langword="null"/> si el intento no fue rechazado o nadie la determinó.
    /// </summary>
    string? FailureCause { get; }

    /// <summary>
    /// Registra la causa interna del rechazo. Si se llama más de una vez, conserva la última.
    /// </summary>
    /// <param name="cause">Causa interna, una de las constantes de <see cref="AuthFailureCauses"/>.</param>
    void RecordFailure(string cause);
}
