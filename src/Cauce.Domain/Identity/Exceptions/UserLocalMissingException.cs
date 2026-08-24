using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Identity.Exceptions;

/// <summary>
/// Se lanza cuando el proveedor de identidad autentica correctamente unas credenciales pero no existe
/// la cuenta local correspondiente. Es un estado de inconsistencia entre Keycloak y la base de datos,
/// no un error atribuible al cliente: indica un aprovisionamiento que quedó a medias.
/// </summary>
public sealed class UserLocalMissingException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con su mensaje canónico.
    /// </summary>
    public UserLocalMissingException()
        : base("La autenticación fue exitosa pero no existe una cuenta local asociada.")
    {
    }
}
