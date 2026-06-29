using Cauce.Domain.ClinicalRegistry;

namespace Cauce.Application.Common.Interfaces.ClinicalRegistry;

/// <summary>
/// Repositorio del agregado <see cref="ClinicalNote"/> (notas clínicas).
/// </summary>
public interface IClinicalNoteRepository
{
    /// <summary>
    /// Busca una nota clínica por su identificador.
    /// </summary>
    /// <param name="noteId">Identificador de la nota.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La nota o <see langword="null"/> si no existe.</returns>
    Task<ClinicalNote?> FindByIdAsync(Guid noteId, CancellationToken ct = default);

    /// <summary>
    /// Lista las notas clínicas del paciente en un rango de fechas.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="from">Inicio del rango.</param>
    /// <param name="to">Fin del rango.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Las notas clínicas del paciente.</returns>
    Task<IReadOnlyList<ClinicalNote>> ListByPatientInRangeAsync(Guid patientId, DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// Agrega una nueva nota clínica al contexto de persistencia.
    /// </summary>
    /// <param name="note">Nota a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(ClinicalNote note, CancellationToken ct = default);
}
