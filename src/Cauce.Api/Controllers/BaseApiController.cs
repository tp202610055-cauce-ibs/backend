using Asp.Versioning;
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
}
