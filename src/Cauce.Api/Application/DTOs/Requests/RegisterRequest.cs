using System.ComponentModel.DataAnnotations;

namespace Cauce.Api.Application.DTOs.Requests;

/// <summary>
/// Payload de registro de un nuevo paciente. Implementa los criterios de
/// aceptación CA01 y CA02 del US01 (Registro de Paciente) del Product Backlog v5.
/// </summary>
public class RegisterRequest
{
    /// <summary>Nombre completo del paciente.</summary>
    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "El nombre completo debe tener entre 2 y 150 caracteres.")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Correo electrónico. Funciona como identificador único del usuario.</summary>
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido.")]
    [StringLength(150, ErrorMessage = "El correo electrónico no puede superar los 150 caracteres.")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Contraseña en texto plano. La política exige mínimo 8 caracteres,
    /// al menos una mayúscula y al menos un número (US01 CA01).
    /// </summary>
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [RegularExpression(
        @"^(?=.*[A-Z])(?=.*\d).+$",
        ErrorMessage = "La contraseña debe contener al menos una letra mayúscula y un número.")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Indica si el usuario aceptó el consentimiento informado. Obligatorio
    /// por US01 CA03 y por la Ley N° 29733. Sin aceptación, el registro no procede.
    /// </summary>
    [Required(ErrorMessage = "Debe indicar si acepta el consentimiento informado.")]
    public bool ConsentAccepted { get; set; }
}