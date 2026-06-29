using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.DeleteCustomFood;

/// <summary>
/// Comando para eliminar un alimento personalizado del paciente autenticado. Solo se
/// permite si el alimento no está referenciado por ítems de comida.
/// </summary>
/// <param name="CustomFoodId">Identificador del alimento personalizado a eliminar.</param>
public sealed record DeleteCustomFoodCommand(Guid CustomFoodId) : IRequest;
