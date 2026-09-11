using MediatR;

namespace Cauce.Application.Identity.UseCases.GetNutritionist;

/// <summary>
/// Consulta administrativa del resumen de una cuenta de nutricionista (acta A49).
/// </summary>
/// <param name="NutritionistId">Identificador local de la cuenta.</param>
public sealed record GetNutritionistQuery(Guid NutritionistId) : IRequest<GetNutritionistResult>;
