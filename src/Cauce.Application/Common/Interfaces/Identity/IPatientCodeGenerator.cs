using Cauce.Domain.Identity;

namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Generador del código correlativo legible de un paciente (<c>PAC-0042</c>). La implementación debe
/// garantizar que dos altas simultáneas nunca reciban el mismo código.
/// </summary>
public interface IPatientCodeGenerator
{
    /// <summary>
    /// Reserva y devuelve el próximo código de paciente disponible.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El código de paciente reservado.</returns>
    Task<PatientCode> NextAsync(CancellationToken ct = default);
}
