using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeder idempotente del catálogo cerrado de alergias e intolerancias relevantes
/// para SII. Inserta las entradas que aún no existen.
/// </summary>
public sealed class AllergiesSeeder
{
    private static readonly IReadOnlyList<(string Name, AllergyType Type, string Description)> Catalog = new[]
    {
        ("Gluten", AllergyType.Intolerance, "Intolerancia a la proteína del trigo, cebada y centeno."),
        ("Lactosa", AllergyType.Intolerance, "Incapacidad de digerir el azúcar de la leche por déficit de lactasa."),
        ("Frutos secos", AllergyType.Allergy, "Reacción inmunológica a almendras, nueces, maní, avellanas y similares."),
        ("Mariscos", AllergyType.Allergy, "Reacción inmunológica a camarones, langostinos, cangrejos, mejillones y similares."),
        ("Huevo", AllergyType.Allergy, "Reacción inmunológica a proteínas del huevo de gallina."),
        ("Soya", AllergyType.Allergy, "Reacción inmunológica a derivados de la soya."),
        ("Pescado", AllergyType.Allergy, "Reacción inmunológica a pescados de mar y río."),
        ("Sulfitos", AllergyType.Sensitivity, "Reacción adversa a conservantes con sulfitos."),
        ("Leguminosas", AllergyType.Sensitivity, "Reacción adversa a frijoles, lentejas, garbanzos y arvejas."),
        ("Fructosa", AllergyType.Intolerance, "Malabsorción de fructosa, frecuentemente asociada a síntomas de SII.")
    };

    private readonly CauceDbContext _context;
    private readonly ILogger<AllergiesSeeder> _logger;

    /// <summary>
    /// Inicializa el seeder con sus dependencias.
    /// </summary>
    /// <param name="context">Contexto de base de datos.</param>
    /// <param name="logger">Logger de la categoría del seeder.</param>
    public AllergiesSeeder(CauceDbContext context, ILogger<AllergiesSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Siembra el catálogo de alergias de forma idempotente.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var added = 0;
        foreach (var (name, type, description) in Catalog)
        {
            var exists = await _context.Set<Allergy>()
                .AnyAsync(x => x.Name == name, ct)
                .ConfigureAwait(false);

            if (!exists)
            {
                _context.Set<Allergy>().Add(Allergy.SeedEntry(Guid.NewGuid(), name, type, description));
                added++;
            }
        }

        if (added > 0)
        {
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("Seeded {Count} allergy catalog entries.", added);
        }
    }
}
