using Cauce.Domain.ClinicalRegistry;

namespace Cauce.Application.Common.Interfaces.ClinicalRegistry;

/// <summary>
/// Repositorio de solo lectura del glosario clínico-nutricional (US27).
/// </summary>
public interface IGlossaryRepository
{
    /// <summary>
    /// Lista todos los términos del glosario ordenados alfabéticamente por término.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Los términos del glosario.</returns>
    Task<IReadOnlyList<GlossaryTerm>> ListAllOrderedAsync(CancellationToken ct = default);

    /// <summary>
    /// Busca términos del glosario cuyo término o definiciones coincidan con el texto indicado. La
    /// búsqueda es insensible a mayúsculas y a tildes.
    /// </summary>
    /// <param name="query">Texto a buscar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Los términos coincidentes, ordenados por término.</returns>
    Task<IReadOnlyList<GlossaryTerm>> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Indica si el glosario ya tiene ese término exacto, sin distinguir mayúsculas ni tildes.
    /// </summary>
    /// <param name="term">Término a comprobar, ya recortado.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns><see langword="true"/> si el término ya existe.</returns>
    Task<bool> ExistsByTermAsync(string term, CancellationToken ct = default);
}
