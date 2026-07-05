using System.Security.Cryptography;
using System.Text;

namespace Cauce.Application.Common.Auditing;

/// <summary>
/// Calcula huellas SHA-256 para la bitácora de auditoría. Permite registrar de forma
/// tamper-evidente valores sensibles (notas clínicas, motivos de rechazo) sin guardar su
/// texto en claro, en coherencia con la Ley N.° 29733.
/// </summary>
public static class AuditHash
{
    /// <summary>
    /// Calcula el hash SHA-256 del texto indicado y lo devuelve en hexadecimal en mayúsculas.
    /// </summary>
    /// <param name="input">Texto a resumir.</param>
    /// <returns>Hash SHA-256 en hexadecimal.</returns>
    public static string Sha256Hex(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash);
    }
}
