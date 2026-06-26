using Cauce.Domain.Identity.Enums;

namespace Cauce.Application.Identity.UseCases.RegisterPatient;

/// <summary>
/// Resultado del registro de un paciente.
/// </summary>
/// <param name="UserId">Identificador del usuario creado.</param>
/// <param name="Email">Correo electrónico del usuario.</param>
/// <param name="Status">Estado del usuario tras el registro.</param>
/// <param name="EmailVerificationRequired">Indica si el usuario debe verificar su correo.</param>
public sealed record RegisterPatientResult(
    Guid UserId,
    string Email,
    UserStatus Status,
    bool EmailVerificationRequired);
