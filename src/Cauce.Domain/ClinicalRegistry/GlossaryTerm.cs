using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Common;

namespace Cauce.Domain.ClinicalRegistry;

/// <summary>
/// Término del glosario clínico-nutricional del piloto (US27). Cada término tiene dos definiciones,
/// una en lenguaje llano para el paciente y otra técnica para el nutricionista, y una categoría. El
/// contenido está en estado de borrador pendiente de validación clínica (acta A27).
/// </summary>
public sealed class GlossaryTerm : Entity, IAggregateRoot
{
    /// <summary>
    /// Término o sigla (único, por ejemplo "FODMAP", "IBS-SSS").
    /// </summary>
    public string Term { get; private set; } = string.Empty;

    /// <summary>
    /// Definición en lenguaje llano dirigida al paciente.
    /// </summary>
    public string PatientDefinition { get; private set; } = string.Empty;

    /// <summary>
    /// Definición técnica dirigida al nutricionista.
    /// </summary>
    public string NutritionistDefinition { get; private set; } = string.Empty;

    /// <summary>
    /// Categoría del término.
    /// </summary>
    public GlossaryCategory Category { get; private set; }

    /// <summary>
    /// Momento de creación, en UTC.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Momento de la última modificación, en UTC.
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    private GlossaryTerm()
    {
    }

    private GlossaryTerm(
        Guid id,
        string term,
        string patientDefinition,
        string nutritionistDefinition,
        GlossaryCategory category,
        DateTime utcNow)
        : base(id)
    {
        Term = term;
        PatientDefinition = patientDefinition;
        NutritionistDefinition = nutritionistDefinition;
        Category = category;
        CreatedAt = utcNow;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Crea una entrada del glosario para el seed inicial.
    /// </summary>
    /// <param name="id">Identificador del término.</param>
    /// <param name="term">Término o sigla.</param>
    /// <param name="patientDefinition">Definición para el paciente.</param>
    /// <param name="nutritionistDefinition">Definición para el nutricionista.</param>
    /// <param name="category">Categoría del término.</param>
    /// <param name="utcNow">Marca de tiempo UTC de creación.</param>
    /// <returns>La nueva entrada del glosario.</returns>
    public static GlossaryTerm SeedEntry(
        Guid id,
        string term,
        string patientDefinition,
        string nutritionistDefinition,
        GlossaryCategory category,
        DateTime utcNow)
    {
        return new GlossaryTerm(id, term, patientDefinition, nutritionistDefinition, category, utcNow);
    }
}
