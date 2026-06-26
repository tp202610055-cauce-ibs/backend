namespace Cauce.Application.Common.Exceptions;

/// <summary>
/// Representa un fallo al integrarse con la Admin API de Keycloak. No es una
/// excepción de dominio: indica una falla de la dependencia externa de identidad.
/// El mensaje nunca contiene datos personales.
/// </summary>
public sealed class KeycloakIntegrationException : Exception
{
    /// <summary>
    /// Código de estado HTTP devuelto por Keycloak, si está disponible.
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// Inicializa la excepción con un mensaje descriptivo.
    /// </summary>
    /// <param name="message">Mensaje sin datos personales.</param>
    public KeycloakIntegrationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Inicializa la excepción con un mensaje y el código de estado HTTP de Keycloak.
    /// </summary>
    /// <param name="message">Mensaje sin datos personales.</param>
    /// <param name="statusCode">Código de estado HTTP devuelto por Keycloak.</param>
    public KeycloakIntegrationException(string message, int statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Inicializa la excepción con un mensaje y la excepción interna que la originó.
    /// </summary>
    /// <param name="message">Mensaje sin datos personales.</param>
    /// <param name="innerException">Excepción interna.</param>
    public KeycloakIntegrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
