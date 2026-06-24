using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cauce.Api.Controllers;

/// <summary>
/// Controlador de salud de la API. Expone un endpoint público para verificar que
/// el servicio está operativo.
/// </summary>
public sealed class HealthController : BaseApiController
{
    /// <summary>
    /// Devuelve el estado de salud del servicio junto con la marca de tiempo UTC.
    /// </summary>
    /// <returns>Objeto con el estado y la marca de tiempo en formato ISO 8601.</returns>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow.ToString("o")
        });
    }
}
