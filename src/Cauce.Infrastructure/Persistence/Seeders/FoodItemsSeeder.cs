using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Persistence.Seeders;

// TODO (TS12): Reemplazar este catálogo inicial de alimentos peruanos representativos por
// el dataset completo TPCA-CENAN (~928 alimentos) cuando esté disponible. Coordinar con
// Mirian Contreras para la entrega del CSV. El esquema de FoodItem ya soporta el dataset
// completo; la migración será simplemente cargar más filas vía este seeder o vía un job de
// importación dedicado.

/// <summary>
/// Seeder idempotente del catálogo inicial de alimentos. Inserta las entradas que aún no
/// existen (verificadas por nombre). Es un subconjunto representativo suficiente para las
/// pruebas de integración y la demo del piloto.
/// </summary>
public sealed class FoodItemsSeeder
{
    private static readonly IReadOnlyList<FoodSeed> Catalog = new[]
    {
        // Cereales
        new FoodSeed("Arroz blanco cocido", "cereales", 130, 2.7m, 28.2m, 0.3m, 0.4m, FodmapLevel.Low, null, false),
        new FoodSeed("Quinua blanca cocida", "cereales", 120, 4.4m, 21.3m, 1.9m, 2.8m, FodmapLevel.Low, null, true),
        new FoodSeed("Avena en hojuelas cocida", "cereales", 71, 2.5m, 12.0m, 1.5m, 1.7m, FodmapLevel.Moderate, "fructans", false),
        new FoodSeed("Pan de trigo blanco", "cereales", 265, 9.0m, 49.0m, 3.2m, 2.7m, FodmapLevel.High, "fructans", false),

        // Proteínas
        new FoodSeed("Pollo a la plancha", "proteinas", 165, 31.0m, 0.0m, 3.6m, 0.0m, FodmapLevel.Low, null, false),
        new FoodSeed("Pescado bonito", "proteinas", 140, 23.0m, 0.0m, 5.0m, 0.0m, FodmapLevel.Low, null, true),
        new FoodSeed("Huevo de gallina cocido", "proteinas", 155, 12.5m, 1.1m, 11.0m, 0.0m, FodmapLevel.Low, null, false),
        new FoodSeed("Carne de res magra", "proteinas", 250, 26.0m, 0.0m, 15.0m, 0.0m, FodmapLevel.Low, null, false),

        // Lácteos
        new FoodSeed("Leche entera de vaca", "lacteos", 61, 3.2m, 4.8m, 3.3m, 0.0m, FodmapLevel.High, "lactose", false),
        new FoodSeed("Yogur natural sin lactosa", "lacteos", 60, 4.0m, 6.0m, 2.0m, 0.0m, FodmapLevel.Low, null, false),
        new FoodSeed("Queso fresco", "lacteos", 264, 17.0m, 3.0m, 21.0m, 0.0m, FodmapLevel.Moderate, "lactose", true),

        // Frutas
        new FoodSeed("Plátano de la isla maduro", "frutas", 89, 1.1m, 22.8m, 0.3m, 2.6m, FodmapLevel.Low, null, true),
        new FoodSeed("Manzana red delicious", "frutas", 52, 0.3m, 14.0m, 0.2m, 2.4m, FodmapLevel.High, "fructans,polyols", false),
        new FoodSeed("Naranja", "frutas", 47, 0.9m, 11.8m, 0.1m, 2.4m, FodmapLevel.Low, null, false),
        new FoodSeed("Papaya", "frutas", 43, 0.5m, 11.0m, 0.3m, 1.7m, FodmapLevel.Low, null, true),

        // Verduras
        new FoodSeed("Papa amarilla cocida", "verduras", 87, 1.9m, 20.1m, 0.1m, 1.8m, FodmapLevel.Low, null, true),
        new FoodSeed("Camote sancochado", "verduras", 86, 1.6m, 20.1m, 0.1m, 3.0m, FodmapLevel.Moderate, "polyols", true),
        new FoodSeed("Zanahoria cocida", "verduras", 35, 0.8m, 8.2m, 0.2m, 2.8m, FodmapLevel.Low, null, false),
        new FoodSeed("Cebolla cocida", "verduras", 40, 1.1m, 9.3m, 0.1m, 1.7m, FodmapLevel.High, "fructans", false),
        new FoodSeed("Brócoli cocido", "verduras", 35, 2.4m, 7.0m, 0.4m, 3.3m, FodmapLevel.Moderate, "fructans", false),

        // Leguminosas
        new FoodSeed("Lentejas cocidas", "leguminosas", 116, 9.0m, 20.1m, 0.4m, 7.9m, FodmapLevel.High, "oligos", false),
        new FoodSeed("Frejol canario cocido", "leguminosas", 127, 8.7m, 22.8m, 0.5m, 6.4m, FodmapLevel.High, "oligos", true),

        // Preparaciones
        new FoodSeed("Aji de gallina", "preparaciones", 185, 12.5m, 14.0m, 9.5m, 1.8m, FodmapLevel.Moderate, "lactose", true),
        new FoodSeed("Lomo saltado", "preparaciones", 210, 16.0m, 18.0m, 8.5m, 2.0m, FodmapLevel.Moderate, "fructans", true),
        new FoodSeed("Ceviche de pescado", "preparaciones", 120, 18.0m, 6.0m, 2.5m, 1.0m, FodmapLevel.Low, null, true),
        new FoodSeed("Causa rellena", "preparaciones", 165, 4.0m, 24.0m, 5.0m, 2.5m, FodmapLevel.Low, null, true)
    };

    private readonly CauceDbContext _context;
    private readonly ILogger<FoodItemsSeeder> _logger;

    /// <summary>
    /// Inicializa el seeder con sus dependencias.
    /// </summary>
    /// <param name="context">Contexto de base de datos.</param>
    /// <param name="logger">Logger de la categoría del seeder.</param>
    public FoodItemsSeeder(CauceDbContext context, ILogger<FoodItemsSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Cantidad de alimentos del catálogo inicial.
    /// </summary>
    public static int CatalogSize => Catalog.Count;

    /// <summary>
    /// Siembra el catálogo de alimentos de forma idempotente.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var utcNow = DateTime.UtcNow;
        var added = 0;

        foreach (var seed in Catalog)
        {
            var exists = await _context.Set<FoodItem>()
                .AnyAsync(x => x.Name == seed.Name, ct)
                .ConfigureAwait(false);

            if (!exists)
            {
                _context.Set<FoodItem>().Add(FoodItem.SeedEntry(
                    Guid.NewGuid(), seed.Name, seed.Category, seed.Calories, seed.Protein,
                    seed.Carbs, seed.Fat, seed.Fiber, seed.Level, seed.Tags, seed.IsPeruvian, utcNow));
                added++;
            }
        }

        if (added > 0)
        {
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("Seeded {Count} food catalog entries.", added);
        }
    }

    /// <summary>
    /// Entrada del catálogo inicial de alimentos.
    /// </summary>
    /// <param name="Name">Nombre del alimento.</param>
    /// <param name="Category">Categoría controlada.</param>
    /// <param name="Calories">Energía por 100 g.</param>
    /// <param name="Protein">Proteína por 100 g.</param>
    /// <param name="Carbs">Carbohidratos por 100 g.</param>
    /// <param name="Fat">Grasa por 100 g.</param>
    /// <param name="Fiber">Fibra por 100 g.</param>
    /// <param name="Level">Nivel de carga FODMAP.</param>
    /// <param name="Tags">Etiquetas FODMAP, o <see langword="null"/>.</param>
    /// <param name="IsPeruvian">Indica si el alimento es peruano.</param>
    private sealed record FoodSeed(
        string Name,
        string Category,
        decimal Calories,
        decimal Protein,
        decimal Carbs,
        decimal Fat,
        decimal Fiber,
        FodmapLevel Level,
        string? Tags,
        bool IsPeruvian);
}
