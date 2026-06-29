namespace Cauce.Application.Common.Exceptions;

/// <summary>
/// Se lanza cuando un comando idempotente reusa un <c>client_guid</c> ya procesado pero
/// con una carga distinta. En un contexto clínico, un reuso con carga diferente es un
/// error del cliente que el servidor debe rechazar (409) en lugar de devolver el
/// resultado previo como si fuera nuevo.
/// </summary>
public sealed class IdempotencyMismatchException : Exception
{
    /// <summary>
    /// Inicializa la excepción.
    /// </summary>
    public IdempotencyMismatchException()
        : base("La clave de idempotencia ya fue utilizada con una carga distinta.")
    {
    }
}
