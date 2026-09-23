using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace Cauce.Api.Controllers;

/// <summary>
/// Controlador base del que heredan todos los controladores de la API. Aplica el
/// versionado en la URL (<c>/api/v{version}/...</c>) y las convenciones de API
/// definidas en las decisiones del Bloque 3 (DEC-B3-05).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// Nombre del encabezado que transporta la clave de idempotencia (DEC-B3-04).
    /// </summary>
    protected const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>
    /// Resuelve la clave de idempotencia de una creación, que puede llegar en el cuerpo
    /// (<c>clientGuid</c>) o en el encabezado <c>Idempotency-Key</c>. Si viajan ambas y difieren,
    /// se rechaza: aceptar una de las dos en silencio haría que el cliente creyera haber
    /// desduplicado por una clave que el servidor ignoró.
    /// </summary>
    /// <param name="bodyClientGuid">Valor presente en el cuerpo, o <see langword="null"/>.</param>
    /// <returns>La clave de idempotencia resuelta, o <see cref="Guid.Empty"/> si no llegó ninguna.</returns>
    /// <exception cref="ValidationException">Si el cuerpo y el encabezado no coinciden.</exception>
    protected Guid ResolveClientGuid(Guid? bodyClientGuid)
    {
        Guid? headerGuid = null;
        if (Request.Headers.TryGetValue(IdempotencyKeyHeader, out var headerValues)
            && Guid.TryParse(headerValues.ToString(), out var parsed))
        {
            headerGuid = parsed;
        }

        if (bodyClientGuid.HasValue && headerGuid.HasValue && bodyClientGuid.Value != headerGuid.Value)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure("clientGuid", "El Idempotency-Key no coincide con el client_guid del cuerpo.")
            });
        }

        return bodyClientGuid ?? headerGuid ?? Guid.Empty;
    }
}
