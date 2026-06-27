using Cauce.Domain.Patients;

namespace Cauce.Application.Common.Interfaces.Patients;

/// <summary>
/// Repositorio del agregado <see cref="PatientAllergy"/>.
/// </summary>
public interface IPatientAllergyRepository
{
    /// <summary>
    /// Lista las alergias declaradas por un paciente.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Lista de declaraciones de alergia del paciente.</returns>
    Task<IReadOnlyList<PatientAllergy>> ListByPatientAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Busca una declaración de alergia por su identificador.
    /// </summary>
    /// <param name="patientAllergyId">Identificador de la declaración.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La declaración o <see langword="null"/> si no existe.</returns>
    Task<PatientAllergy?> FindByIdAsync(Guid patientAllergyId, CancellationToken ct = default);

    /// <summary>
    /// Indica si el paciente ya declaró la alergia indicada.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="allergyId">Identificador de la alergia.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns><see langword="true"/> si la declaración ya existe.</returns>
    Task<bool> ExistsAsync(Guid patientId, Guid allergyId, CancellationToken ct = default);

    /// <summary>
    /// Agrega una nueva declaración al contexto de persistencia.
    /// </summary>
    /// <param name="patientAllergy">Declaración a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(PatientAllergy patientAllergy, CancellationToken ct = default);

    /// <summary>
    /// Elimina una declaración de alergia del contexto de persistencia.
    /// </summary>
    /// <param name="patientAllergy">Declaración a eliminar.</param>
    void Remove(PatientAllergy patientAllergy);
}
