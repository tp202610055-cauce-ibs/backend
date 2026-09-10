namespace Cauce.Api.Contracts.Patients;

/// <summary>
/// Cuerpo de la petición de canje de un código de invitación después del registro (acta A41). Permite a
/// un paciente que se registró sin código vincularse a un nutricionista más adelante.
/// </summary>
/// <param name="InvitationCode">
/// Código de invitación emitido por el nutricionista. Se normaliza a mayúsculas sin espacios
/// envolventes antes de validarse.
/// </param>
public sealed record AssignNutritionistRequest(string InvitationCode);
