using Cauce.Domain.Patients;

namespace Cauce.Application.Common.Interfaces.Patients;

/// <summary>
/// Repositorio del catálogo de alergias (<see cref="Allergy"/>).
/// </summary>
public interface IAllergyRepository
{
    /// <summary>
    /// Lista las alergias activas del catálogo.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Lista de alergias activas.</returns>
    Task<IReadOnlyList<Allergy>> ListActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Busca una alergia por su identificador.
    /// </summary>
    /// <param name="allergyId">Identificador de la alergia.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La alergia o <see langword="null"/> si no existe.</returns>
    Task<Allergy?> FindByIdAsync(Guid allergyId, CancellationToken ct = default);

    /// <summary>
    /// Agrega una nueva entrada al catálogo.
    /// </summary>
    /// <param name="allergy">Entrada a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(Allergy allergy, CancellationToken ct = default);
}
