namespace Cauce.Api.Application.Exceptions;

/// <summary>
/// Excepción lanzada cuando no se encuentra un usuario por su identificador.
/// Se mapea a HTTP 404 en la capa de presentación.
/// </summary>
public class UserNotFoundException : Exception
{
    /// <summary>
    /// Inicializa una nueva instancia de UserNotFoundException con el identificador buscado.
    /// </summary>
    /// <param name="userId">Identificador del usuario no encontrado.</param>
    public UserNotFoundException(Guid userId)
        : base($"No se encontró el usuario con identificador {userId}.") { }
}