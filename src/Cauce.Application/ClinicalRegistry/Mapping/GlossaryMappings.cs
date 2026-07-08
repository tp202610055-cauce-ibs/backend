using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Domain.ClinicalRegistry;

namespace Cauce.Application.ClinicalRegistry.Mapping;

/// <summary>
/// Proyecciones del glosario clínico (US27) a sus DTO, seleccionando la definición según el rol.
/// </summary>
internal static class GlossaryMappings
{
    /// <summary>
    /// Estado del contenido del glosario: borrador pendiente de validación clínica con el nutricionista
    /// del Kaelín (acta A27).
    /// </summary>
    public const string DraftContentStatus = "draft-pending-clinical-review";

    /// <summary>
    /// Proyecta los términos al resultado del glosario con la definición apropiada al rol.
    /// </summary>
    /// <param name="terms">Términos del glosario.</param>
    /// <param name="isNutritionist">Indica si el solicitante es nutricionista.</param>
    /// <returns>El resultado del glosario.</returns>
    public static GlossaryResult ToResult(IReadOnlyList<GlossaryTerm> terms, bool isNutritionist)
    {
        var dtos = terms
            .Select(term => new GlossaryTermDto(
                term.Term,
                isNutritionist ? term.NutritionistDefinition : term.PatientDefinition,
                term.Category))
            .ToList();

        return new GlossaryResult(dtos, DraftContentStatus);
    }
}
