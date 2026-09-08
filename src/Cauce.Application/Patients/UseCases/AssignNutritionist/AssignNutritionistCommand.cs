using MediatR;

namespace Cauce.Application.Patients.UseCases.AssignNutritionist;

/// <summary>
/// Comando de canje de un código de invitación después del registro (acta A41). El paciente se resuelve
/// del contexto de la petición, no del cuerpo, así que un paciente no puede canjear en nombre de otro.
/// </summary>
/// <param name="InvitationCode">Código de invitación, ya normalizado por el controlador.</param>
public sealed record AssignNutritionistCommand(string InvitationCode) : IRequest<AssignNutritionistResult>;

/// <summary>
/// Resultado del canje: los datos del nutricionista con el que quedó vinculado el paciente.
/// </summary>
/// <param name="NutritionistId">Identificador de la cuenta del nutricionista.</param>
/// <param name="NutritionistFullName">Nombre completo del nutricionista.</param>
/// <param name="AssignedAt">Momento de la asignación, en UTC.</param>
public sealed record AssignNutritionistResult(
    Guid NutritionistId,
    string NutritionistFullName,
    DateTime AssignedAt);
