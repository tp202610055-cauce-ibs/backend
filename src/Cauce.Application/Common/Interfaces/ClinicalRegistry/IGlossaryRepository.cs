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
}
