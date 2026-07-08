using MediatR;

namespace Cauce.Application.Patients.UseCases.DeleteMyAccount;

/// <summary>
/// Comando para eliminar (anonimizar) la cuenta del paciente autenticado en ejercicio del derecho al
/// olvido (US26, Ley N° 29733).
/// </summary>
/// <param name="ConfirmedActivePilotAcknowledged">
/// Indica si el paciente acusó explícitamente la retención normativa de sus datos cuando su cuenta
/// está inscrita en un piloto clínico activo. Si la cuenta está en el piloto y este acuse es
/// <see langword="false"/>, la operación se rechaza (US26 CA02).
/// </param>
public sealed record DeleteMyAccountCommand(bool ConfirmedActivePilotAcknowledged) : IRequest<Unit>;
