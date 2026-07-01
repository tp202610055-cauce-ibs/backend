using Cauce.Application.Recommendations.Contracts;

namespace Cauce.Application.Common.Interfaces.Recommendations;

/// <summary>
/// Lector del perfil del paciente que proyecta sus datos clínicos a la instantánea de contexto
/// que consume el motor y el orquestador de explicaciones.
/// </summary>
public interface IPatientProfileReader
{
    /// <summary>
    /// Obtiene el contexto clínico del paciente.
    /// </summary>
    /// <param name="patientId">Identificador del paciente (cuenta de usuario).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El contexto del paciente o <see langword="null"/> si no tiene perfil.</returns>
    Task<PatientContextSnapshot?> GetPatientContextAsync(Guid patientId, CancellationToken ct = default);
}
