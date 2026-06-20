namespace Cauce.Api.Application.DTOs.Responses;

/// <summary>
/// Respuesta de autenticación. Retornada tanto en registro exitoso (US01 CA01)
/// como en login exitoso (US05 CA01). Incluye el JWT firmado con el que el
/// cliente debe autenticarse en peticiones subsiguientes.
/// </summary>
public class AuthResponse
{
    /// <summary>Identificador único del usuario autenticado.</summary>
    public Guid UserId { get; set; }

    /// <summary>Nombre completo del usuario.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Correo electrónico del usuario.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Rol asignado: "patient" o "nutritionist".</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Token JWT firmado para autenticación de peticiones subsiguientes.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Momento exacto de expiración del token. UTC.</summary>
    public DateTime ExpiresAt { get; set; }
}