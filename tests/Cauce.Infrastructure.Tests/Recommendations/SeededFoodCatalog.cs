using System.Collections;
using System.Reflection;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Infrastructure.Persistence.Seeders;

namespace Cauce.Infrastructure.Tests.Recommendations;

/// <summary>
/// Lee por reflexión el catálogo privado de <see cref="FoodItemsSeeder"/>, que no expone las etiquetas.
/// </summary>
internal static class SeededFoodCatalog
{
    /// <summary>
    /// Devuelve los alimentos que siembra el seeder.
    /// </summary>
    public static IReadOnlyList<SeederFood> Read()
    {
        var field = typeof(FoodItemsSeeder).GetField("Catalog", BindingFlags.NonPublic | BindingFlags.Static)!;
        return ((IEnumerable)field.GetValue(null)!)
            .Cast<object>()
            .Select(entry => new SeederFood(
                (string)ReadProperty(entry, "Name")!,
                (string)ReadProperty(entry, "Category")!,
                (FodmapLevel)ReadProperty(entry, "Level")!,
                (string?)ReadProperty(entry, "Tags")))
            .ToList();
    }

    private static object? ReadProperty(object entry, string property) =>
        entry.GetType().GetProperty(property)!.GetValue(entry);
}

/// <summary>
/// Alimento del catálogo sembrado.
/// </summary>
/// <param name="Name">Nombre.</param>
/// <param name="Category">Categoría.</param>
/// <param name="Level">Nivel FODMAP.</param>
/// <param name="Tags">Etiquetas FODMAP, o <see langword="null"/>.</param>
internal sealed record SeederFood(string Name, string Category, FodmapLevel Level, string? Tags);
