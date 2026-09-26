using Cauce.Application.Common.Idempotency;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.SetSymptomMealAssociation;

/// <summary>
/// Comando para que el nutricionista asignado corrija a mano la comida asociada a un síntoma de su
/// paciente. Con <see cref="MealId"/> fija la asociación a esa comida, sin sujeción a la ventana de
/// 4 horas; con <see langword="null"/> la desvincula. Es idempotente respecto del
/// <see cref="ClientGuid"/> tomado del header <c>Idempotency-Key</c>. El nutricionista se resuelve del JWT.
/// </summary>
/// <param name="SymptomId">Identificador del síntoma a corregir.</param>
/// <param name="MealId">Comida que se asocia, o <see langword="null"/> para desvincular.</param>
/// <param name="ClientGuid">Clave de idempotencia (UUID v4).</param>
public sealed record SetSymptomMealAssociationCommand(
    Guid SymptomId,
    Guid? MealId,
    Guid ClientGuid) : IRequest<Unit>, IIdempotentCommand;
