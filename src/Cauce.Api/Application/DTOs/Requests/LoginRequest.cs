using System.ComponentModel.DataAnnotations;

namespace Cauce.Api.Application.DTOs.Requests;

/// <summary>
/// Payload de inicio de sesión. Implementa los criterios de aceptación
/// CA01 y CA02 del US05 (Inicio de Sesión del Paciente).
/// </summary>
public class LoginRequest
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    public string Password { get; set; } = string.Empty;
}