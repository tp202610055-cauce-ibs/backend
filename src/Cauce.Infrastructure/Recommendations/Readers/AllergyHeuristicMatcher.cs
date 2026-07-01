using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Recommendations.Readers;

/// <summary>
/// Detecta, de forma heurística y <b>deliberadamente conservadora</b>, qué alimentos del catálogo
/// debe evitar un paciente según sus alergias declaradas (DEC-B4-14). El esquema actual no liga
/// alergias a alimentos, por lo que el cruce se hace por substrings del nombre, categoría exacta y
/// etiquetas FODMAP. Ante la duda se excluye el alimento: en contexto clínico, un falso positivo
/// (excluir un alimento seguro) es preferible a un falso negativo (dejar pasar un alérgeno). Se
/// reemplazará por una consulta a la futura tabla <c>allergy_food_items</c> sin tocar el lector ni
/// los handlers.
/// </summary>
public sealed class AllergyHeuristicMatcher
{
    private readonly IReadOnlyDictionary<string, AllergyMatchRule> _rules;
    private readonly ILogger<AllergyHeuristicMatcher> _logger;

    /// <summary>
    /// Inicializa el matcher con su catálogo de reglas heurísticas.
    /// </summary>
    /// <param name="logger">Logger de la categoría del matcher.</param>
    public AllergyHeuristicMatcher(ILogger<AllergyHeuristicMatcher> logger)
    {
        _logger = logger;
        _rules = BuildRules();
    }

    /// <summary>
    /// Devuelve el conjunto de identificadores de alimentos que el paciente debe evitar según las
    /// alergias indicadas.
    /// </summary>
    /// <param name="allergyNames">Nombres de las alergias declaradas por el paciente.</param>
    /// <param name="foods">Catálogo de alimentos a evaluar.</param>
    /// <returns>Identificadores de alimentos a excluir.</returns>
    public HashSet<Guid> GetForbiddenFoodIds(
        IReadOnlyCollection<string> allergyNames,
        IReadOnlyCollection<FoodCatalogEntry> foods)
    {
        var forbidden = new HashSet<Guid>();

        foreach (var allergyName in allergyNames)
        {
            var key = Normalize(allergyName);
            if (!_rules.TryGetValue(key, out var rule))
            {
                _logger.LogWarning(
                    "No hay regla heurística de alergia para '{AllergyName}'; no se excluye ningún alimento por ella.",
                    allergyName);
                continue;
            }

            foreach (var food in foods)
            {
                if (rule.Matches(food))
                {
                    forbidden.Add(food.Id);
                }
            }
        }

        return forbidden;
    }

    private static IReadOnlyDictionary<string, AllergyMatchRule> BuildRules()
    {
        return new Dictionary<string, AllergyMatchRule>(StringComparer.Ordinal)
        {
            ["lactosa"] = new AllergyMatchRule(
                NameSubstrings: new[] { "leche", "lacte", "yogur", "queso", "crema", "mantequilla", "helado" },
                Categories: new[] { "lacteos" },
                TagSubstrings: new[] { "lactose", "lactosa" }),
            ["fructosa"] = new AllergyMatchRule(
                NameSubstrings: new[] { "manzana", "pera", "mango", "cereza", "sandia", "miel" },
                Categories: Array.Empty<string>(),
                TagSubstrings: new[] { "fructose", "fructosa" }),
            ["gluten"] = new AllergyMatchRule(
                NameSubstrings: new[] { "trigo", "pan", "pasta", "fideo", "galleta", "cebada", "centeno", "avena", "harina" },
                Categories: new[] { "cereales" },
                TagSubstrings: Array.Empty<string>()),
            ["leguminosas"] = new AllergyMatchRule(
                NameSubstrings: new[] { "frijol", "frejol", "lenteja", "garbanzo", "haba", "soya", "soja", "arveja", "chocho" },
                Categories: new[] { "leguminosas" },
                TagSubstrings: new[] { "gos", "legume" }),
            ["frutos secos"] = new AllergyMatchRule(
                NameSubstrings: new[] { "nuez", "nueces", "almendra", "mani", "cacahuate", "pistacho", "avellana", "castana" },
                Categories: Array.Empty<string>(),
                TagSubstrings: Array.Empty<string>()),
            ["mariscos"] = new AllergyMatchRule(
                NameSubstrings: new[] { "camaron", "langostino", "langosta", "cangrejo", "ostra", "pulpo", "calamar", "mejillon", "concha" },
                Categories: Array.Empty<string>(),
                TagSubstrings: Array.Empty<string>()),
            ["pescado"] = new AllergyMatchRule(
                NameSubstrings: new[] { "pescado", "salmon", "atun", "trucha", "bonito", "jurel", "merluza", "caballa", "anchov" },
                Categories: Array.Empty<string>(),
                TagSubstrings: Array.Empty<string>()),
            ["huevo"] = new AllergyMatchRule(
                NameSubstrings: new[] { "huevo", "clara de huevo", "yema" },
                Categories: Array.Empty<string>(),
                TagSubstrings: Array.Empty<string>()),
            ["soya"] = new AllergyMatchRule(
                NameSubstrings: new[] { "soya", "soja", "tofu", "tempeh", "edamame" },
                Categories: Array.Empty<string>(),
                TagSubstrings: Array.Empty<string>()),
            ["sulfitos"] = new AllergyMatchRule(
                NameSubstrings: new[] { "vino", "conserva", "deshidratad", "embutido" },
                Categories: Array.Empty<string>(),
                TagSubstrings: new[] { "sulfite", "sulfito" })
        };
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).Trim();
    }

    /// <summary>
    /// Regla heurística de una alergia: si el nombre, la categoría o las etiquetas de un alimento
    /// coinciden con cualquiera de los patrones, el alimento se excluye.
    /// </summary>
    private sealed record AllergyMatchRule(string[] NameSubstrings, string[] Categories, string[] TagSubstrings)
    {
        public bool Matches(FoodCatalogEntry food)
        {
            var name = Normalize(food.Name);
            if (NameSubstrings.Any(substring => name.Contains(substring, StringComparison.Ordinal)))
            {
                return true;
            }

            var category = Normalize(food.Category);
            if (Categories.Any(value => string.Equals(category, value, StringComparison.Ordinal)))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(food.FodmapTags))
            {
                return false;
            }

            var tags = Normalize(food.FodmapTags);
            return TagSubstrings.Any(substring => tags.Contains(substring, StringComparison.Ordinal));
        }
    }
}
