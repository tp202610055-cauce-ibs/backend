using Cauce.Domain.Common;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.Exceptions;

namespace Cauce.Domain.Recommendations;

/// <summary>
/// Ítem de una recomendación: la acción dietética sugerida sobre un alimento del catálogo,
/// con su razonamiento y, en caso de sustitución, el alimento sustituto. Es entidad interna
/// del agregado <see cref="Recommendation"/> y no raíz de agregado.
/// </summary>
public sealed class RecommendationItem : Entity
{
    /// <summary>
    /// Identificador de la recomendación a la que pertenece.
    /// </summary>
    public Guid RecommendationId { get; private set; }

    /// <summary>
    /// Identificador del alimento del catálogo sobre el que recae la acción.
    /// </summary>
    public Guid FoodId { get; private set; }

    /// <summary>
    /// Identificador del alimento sustituto propuesto, o <see langword="null"/> si la acción
    /// no es de sustitución.
    /// </summary>
    public Guid? SubstituteFoodId { get; private set; }

    /// <summary>
    /// Acción dietética sugerida sobre el alimento.
    /// </summary>
    public ActionType ActionType { get; private set; }

    /// <summary>
    /// Razonamiento que justifica la acción, o <see langword="null"/>.
    /// </summary>
    public string? Reasoning { get; private set; }

    private RecommendationItem()
    {
    }

    private RecommendationItem(Guid id, Guid foodId, ActionType actionType, string? reasoning, Guid? substituteFoodId)
        : base(id)
    {
        FoodId = foodId;
        ActionType = actionType;
        Reasoning = reasoning;
        SubstituteFoodId = substituteFoodId;
    }

    /// <summary>
    /// Crea un ítem de recomendación validando las reglas de su alimento sustituto. El ítem
    /// se asocia a su recomendación cuando esta lo incorpora mediante <see cref="Recommendation.Generate"/>.
    /// </summary>
    /// <param name="foodId">Identificador del alimento del catálogo.</param>
    /// <param name="actionType">Acción dietética sugerida.</param>
    /// <param name="reasoning">Razonamiento de la acción, opcional.</param>
    /// <param name="substituteFoodId">Identificador del alimento sustituto, requerido solo en sustituciones.</param>
    /// <returns>El nuevo ítem de recomendación.</returns>
    /// <exception cref="SubstituteFoodMismatchException">Si se violan las reglas del alimento sustituto.</exception>
    public static RecommendationItem Create(Guid foodId, ActionType actionType, string? reasoning, Guid? substituteFoodId)
    {
        if (foodId == Guid.Empty)
        {
            throw new SubstituteFoodMismatchException("el identificador del alimento es obligatorio.");
        }

        if (substituteFoodId.HasValue)
        {
            if (actionType != ActionType.Substitute)
            {
                throw new SubstituteFoodMismatchException("solo un ítem de sustitución puede tener un alimento sustituto.");
            }

            if (substituteFoodId.Value == Guid.Empty)
            {
                throw new SubstituteFoodMismatchException("el identificador del alimento sustituto es inválido.");
            }

            if (substituteFoodId.Value == foodId)
            {
                throw new SubstituteFoodMismatchException("el alimento sustituto no puede ser el mismo alimento.");
            }
        }
        else if (actionType == ActionType.Substitute)
        {
            throw new SubstituteFoodMismatchException("un ítem de sustitución requiere un alimento sustituto.");
        }

        return new RecommendationItem(Guid.NewGuid(), foodId, actionType, reasoning, substituteFoodId);
    }

    /// <summary>
    /// Asocia el ítem a su recomendación contenedora. Lo invoca la raíz de agregado al
    /// incorporar el ítem.
    /// </summary>
    /// <param name="recommendationId">Identificador de la recomendación contenedora.</param>
    internal void AttachTo(Guid recommendationId)
    {
        RecommendationId = recommendationId;
    }
}
