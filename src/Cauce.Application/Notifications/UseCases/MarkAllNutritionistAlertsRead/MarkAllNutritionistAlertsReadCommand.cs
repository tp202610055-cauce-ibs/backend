using MediatR;

namespace Cauce.Application.Notifications.UseCases.MarkAllNutritionistAlertsRead;

/// <summary>
/// Marca como leídos todos los avisos de la bandeja del nutricionista autenticado.
/// </summary>
public sealed record MarkAllNutritionistAlertsReadCommand : IRequest<Unit>;
