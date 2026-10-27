using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Application.Notifications.Dtos;
using Cauce.Domain.Notifications.Enums;
using MediatR;

namespace Cauce.Application.Notifications.UseCases.ListNutritionistAlerts;

/// <summary>
/// Handler de <see cref="ListNutritionistAlertsQuery"/>. Redacta el texto de cada aviso con el nombre
/// actual del paciente.
/// </summary>
public sealed class ListNutritionistAlertsQueryHandler : IRequestHandler<ListNutritionistAlertsQuery, NutritionistAlertListResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly INutritionistAlertRepository _alertRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public ListNutritionistAlertsQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        INutritionistAlertRepository alertRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _alertRepository = alertRepository;
    }

    /// <inheritdoc />
    public async Task<NutritionistAlertListResult> Handle(ListNutritionistAlertsQuery request, CancellationToken cancellationToken)
    {
        var nutritionistId = await NotificationsUserContext
            .ResolveNutritionistIdAsync(_currentUserService, _userRepository, cancellationToken)
            .ConfigureAwait(false);

        var page = await _alertRepository
            .ListPageAsync(nutritionistId, request.Page, request.PageSize, cancellationToken)
            .ConfigureAwait(false);

        var items = page.Rows
            .Select(row => new NutritionistAlertDto(
                row.Id, row.Type, row.PatientId, row.PatientName, row.PatientCode,
                Message(row.Type, row.PatientName), row.OccurredAt, row.ReadAt is not null, row.ReadAt))
            .ToList();

        return new NutritionistAlertListResult(items, request.Page, request.PageSize, page.TotalCount, page.UnreadCount);
    }

    private static string Message(NutritionistAlertType type, string patientName) => type switch
    {
        NutritionistAlertType.PatientLinked => $"{patientName} se vinculó a tu seguimiento.",
        NutritionistAlertType.PatientSubtypeChanged => $"{patientName} cambió su subtipo de SII.",
        _ => patientName
    };
}
