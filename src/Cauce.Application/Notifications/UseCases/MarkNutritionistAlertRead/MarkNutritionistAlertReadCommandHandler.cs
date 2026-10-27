using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Domain.Notifications.Exceptions;
using MediatR;

namespace Cauce.Application.Notifications.UseCases.MarkNutritionistAlertRead;

/// <summary>
/// Handler de <see cref="MarkNutritionistAlertReadCommand"/>. Un aviso ajeno responde igual que uno inexistente.
/// </summary>
public sealed class MarkNutritionistAlertReadCommandHandler : IRequestHandler<MarkNutritionistAlertReadCommand, Unit>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly INutritionistAlertRepository _alertRepository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public MarkNutritionistAlertReadCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        INutritionistAlertRepository alertRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _alertRepository = alertRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(MarkNutritionistAlertReadCommand request, CancellationToken cancellationToken)
    {
        var nutritionistId = await NotificationsUserContext
            .ResolveNutritionistIdAsync(_currentUserService, _userRepository, cancellationToken)
            .ConfigureAwait(false);

        var alert = await _alertRepository
            .FindOwnedAsync(request.AlertId, nutritionistId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NutritionistAlertNotFoundException();

        alert.MarkAsRead(DateTime.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
