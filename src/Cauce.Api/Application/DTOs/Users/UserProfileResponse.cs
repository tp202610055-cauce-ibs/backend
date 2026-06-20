namespace Cauce.Api.Application.DTOs.Users;

/// <summary>
/// Representación pública del perfil del usuario autenticado.
/// Excluye datos sensibles como hash de contraseña, security stamp y
/// demás campos internos de gestión de ASP.NET Core Identity.
/// </summary>
public record UserProfileResponse
{
    /// <summary>Identificador único del usuario.</summary>
    public Guid UserId { get; init; }

    /// <summary>Nombre completo del usuario.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Correo electrónico del usuario.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Indica si el correo ha sido verificado. En MVP siempre true por DEC-011.</summary>
    public bool EmailVerified { get; init; }

    /// <summary>Rol funcional del usuario (patient o nutritionist).</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Estado actual de la cuenta (active, pending_activation, suspended).</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Fecha y hora de creación de la cuenta en UTC.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Fecha y hora del último inicio de sesión en UTC. Nulo si nunca ha iniciado sesión.</summary>
    public DateTime? LastLoginAt { get; init; }
}