using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Domain.ClinicalRegistry;

namespace Cauce.Application.ClinicalRegistry.Mapping;

/// <summary>
/// Mapeos entre las entidades del módulo de registro clínico y sus DTOs de respuesta.
/// </summary>
public static class ClinicalRegistryMappings
{
    /// <summary>
    /// Proyecta un alimento del catálogo a su resumen.
    /// </summary>
    /// <param name="foodItem">Alimento del catálogo.</param>
    /// <returns>El resumen del alimento.</returns>
    public static FoodItemSummary ToSummary(FoodItem foodItem)
    {
        return new FoodItemSummary(
            foodItem.Id,
            foodItem.Name,
            foodItem.Category,
            foodItem.FodmapLevel,
            foodItem.IsPeruvian);
    }

    /// <summary>
    /// Proyecta un alimento del catálogo a su detalle completo.
    /// </summary>
    /// <param name="foodItem">Alimento del catálogo.</param>
    /// <returns>El detalle del alimento.</returns>
    public static FoodItemDetail ToDetail(FoodItem foodItem)
    {
        return new FoodItemDetail(
            foodItem.Id,
            foodItem.Name,
            foodItem.Category,
            foodItem.CaloriesPer100g,
            foodItem.ProteinGPer100g,
            foodItem.CarbsGPer100g,
            foodItem.FatGPer100g,
            foodItem.FiberGPer100g,
            foodItem.FodmapLevel,
            foodItem.FodmapTags,
            foodItem.IsPeruvian,
            foodItem.IsActive);
    }

    /// <summary>
    /// Proyecta un alimento personalizado a su resumen, con sus ingredientes.
    /// </summary>
    /// <param name="customFood">Alimento personalizado.</param>
    /// <returns>El resumen del alimento personalizado.</returns>
    public static CustomFoodSummary ToSummary(CustomFood customFood)
    {
        var ingredients = customFood.Ingredients
            .Select(ingredient => new CustomFoodIngredientSummary(ingredient.FoodId, ingredient.ProportionGrams))
            .ToList();

        return new CustomFoodSummary(
            customFood.Id,
            customFood.Name,
            customFood.PortionSizeGrams,
            customFood.CreatedAt,
            ingredients);
    }

    /// <summary>
    /// Proyecta una comida a su elemento de historial.
    /// </summary>
    /// <param name="meal">Comida.</param>
    /// <returns>El elemento de historial de la comida.</returns>
    public static MealHistoryItem ToHistoryItem(Meal meal)
    {
        var items = meal.Items
            .Select(item => new MealItemSummary(item.FoodId, item.CustomFoodId, item.Quantity, item.Unit))
            .ToList();

        return new MealHistoryItem(
            meal.Id,
            meal.ClientGuid,
            meal.MealTime,
            meal.ConsumedAt,
            meal.ClientCreatedAt,
            meal.SyncStatus,
            items,
            AggregatedFodmap: null);
    }

    /// <summary>
    /// Proyecta un síntoma a su elemento de historial.
    /// </summary>
    /// <param name="symptom">Síntoma.</param>
    /// <returns>El elemento de historial del síntoma.</returns>
    public static SymptomHistoryItem ToHistoryItem(Symptom symptom)
    {
        return new SymptomHistoryItem(
            symptom.Id,
            symptom.ClientGuid,
            symptom.SymptomType,
            symptom.Intensity,
            symptom.OccurredAt,
            symptom.AssociatedMealId,
            symptom.HasMealAssociation,
            symptom.SyncStatus);
    }

    /// <summary>
    /// Proyecta una nota clínica a su resumen.
    /// </summary>
    /// <param name="note">Nota clínica.</param>
    /// <returns>El resumen de la nota.</returns>
    public static ClinicalNoteSummary ToSummary(ClinicalNote note)
    {
        return new ClinicalNoteSummary(note.Id, note.MealId, note.SymptomId, note.Content, note.CreatedAt);
    }

    /// <summary>
    /// Proyecta una evaluación IBS-SSS a su resumen.
    /// </summary>
    /// <param name="assessment">Evaluación IBS-SSS.</param>
    /// <returns>El resumen de la evaluación.</returns>
    public static IbsSssAssessmentSummary ToSummary(IbsSssAssessment assessment)
    {
        return new IbsSssAssessmentSummary(
            assessment.Id,
            assessment.AssessmentType,
            assessment.CycleNumber,
            assessment.PainSeverity,
            assessment.PainFrequency,
            assessment.BloatingSeverity,
            assessment.BowelHabitsDissatisfaction,
            assessment.LifeInterference,
            assessment.TotalScore,
            assessment.SeverityCategory,
            assessment.CompletedAt,
            assessment.NextAssessmentDate);
    }
}
