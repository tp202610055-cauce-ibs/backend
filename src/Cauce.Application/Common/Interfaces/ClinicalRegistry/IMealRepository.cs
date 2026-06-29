using Cauce.Domain.ClinicalRegistry;

namespace Cauce.Application.Common.Interfaces.ClinicalRegistry;

/// <summary>
/// Repositorio del agregado <see cref="Meal"/> (comidas).
/// </summary>
public interface IMealRepository
{
    /// <summary>
    /// Busca una comida por su identificador.
    /// </summary>
    /// <param name="mealId">Identificador de la comida.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La comida o <see langword="null"/> si no existe.</returns>
    Task<Meal?> FindByIdAsync(Guid mealId, CancellationToken ct = default);

    /// <summary>
    /// Busca una comida con sus ítems cargados.
    /// </summary>
    /// <param name="mealId">Identificador de la comida.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La comida con sus ítems, o <see langword="null"/>.</returns>
    Task<Meal?> FindByIdWithItemsAsync(Guid mealId, CancellationToken ct = default);

    /// <summary>
    /// Busca una comida por su <c>client_guid</c> (identidad estable del dispositivo).
    /// </summary>
    /// <param name="clientGuid">Identificador del dispositivo.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La comida o <see langword="null"/> si no existe.</returns>
    Task<Meal?> FindByClientGuidAsync(Guid clientGuid, CancellationToken ct = default);

    /// <summary>
    /// Busca la comida más reciente del paciente cuyo <c>client_created_at</c> cae dentro
    /// de la ventana indicada. Sirve para la correlación temporal síntoma↔comida (4 horas).
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="windowStart">Inicio de la ventana (exclusivo).</param>
    /// <param name="windowEnd">Fin de la ventana (inclusive).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La comida más reciente dentro de la ventana, o <see langword="null"/>.</returns>
    Task<Meal?> FindLatestInWindowAsync(Guid patientId, DateTime windowStart, DateTime windowEnd, CancellationToken ct = default);

    /// <summary>
    /// Lista las comidas del paciente en un rango de fechas, paginadas.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="from">Inicio del rango.</param>
    /// <param name="to">Fin del rango.</param>
    /// <param name="skip">Cantidad de elementos a omitir.</param>
    /// <param name="take">Cantidad de elementos a tomar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La página de comidas con sus ítems.</returns>
    Task<IReadOnlyList<Meal>> ListByPatientInRangeAsync(Guid patientId, DateTime from, DateTime to, int skip, int take, CancellationToken ct = default);

    /// <summary>
    /// Cuenta las comidas del paciente en un rango de fechas.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="from">Inicio del rango.</param>
    /// <param name="to">Fin del rango.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La cantidad de comidas.</returns>
    Task<int> CountByPatientInRangeAsync(Guid patientId, DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// Agrega una nueva comida al contexto de persistencia.
    /// </summary>
    /// <param name="meal">Comida a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(Meal meal, CancellationToken ct = default);
}
