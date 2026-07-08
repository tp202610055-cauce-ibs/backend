namespace Cauce.Application.ClinicalRegistry.Dtos;

/// <summary>
/// Sugerencias de alimentos para el paciente (US09 CA03): los alimentos del catálogo que consume con
/// más frecuencia, los que registró recientemente y una selección rotativa del catálogo. Solo incluye
/// alimentos del catálogo (no personalizados).
/// </summary>
/// <param name="FrequentLast30Days">Hasta 10 alimentos más frecuentes en los últimos 30 días.</param>
/// <param name="RecentLast24Hours">Hasta 10 alimentos registrados en las últimas 24 horas, del más reciente al más antiguo.</param>
/// <param name="CatalogSuggestions">Hasta 10 sugerencias del catálogo, deterministas por paciente y semana del año.</param>
public sealed record FoodSuggestionsResult(
    IReadOnlyList<FoodItemSummary> FrequentLast30Days,
    IReadOnlyList<FoodItemSummary> RecentLast24Hours,
    IReadOnlyList<FoodItemSummary> CatalogSuggestions);
