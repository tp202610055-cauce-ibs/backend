using MediatR;

namespace Cauce.Application.Notifications.UseCases.MarkNutritionistAlertRead;

/// <summary>
/// Marca como leído un aviso de la bandeja del nutricionista autenticado.
/// </summary>
/// <param name="AlertId">Identificador del aviso.</param>
public sealed record MarkNutritionistAlertReadCommand(Guid AlertId) : IRequest<Unit>;
