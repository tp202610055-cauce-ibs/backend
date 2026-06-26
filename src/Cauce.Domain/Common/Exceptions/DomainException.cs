namespace Cauce.Domain.Common.Exceptions;

/// <summary>
/// Clase base abstracta para todas las excepciones de dominio. Representa una
/// violación de una invariante o regla de negocio. Las excepciones concretas de
/// cada módulo heredan de esta clase.
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>
    /// Inicializa la excepción con el mensaje especificado.
    /// </summary>
    /// <param name="message">Descripción de la violación de dominio.</param>
    protected DomainException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Inicializa la excepción con el mensaje y la excepción interna especificados.
    /// </summary>
    /// <param name="message">Descripción de la violación de dominio.</param>
    /// <param name="innerException">Excepción que originó esta.</param>
    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
