using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using Cauce.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.ClinicalRegistry.UseCases.DeleteCustomFood;

/// <summary>
/// Handler de la eliminación de un alimento personalizado. Verifica la propiedad del
/// paciente y que el alimento no esté referenciado por ítems de comida; registra el
/// evento en auditoría.
/// </summary>
public sealed class DeleteCustomFoodCommandHandler : IRequestHandler<DeleteCustomFoodCommand>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly ICustomFoodRepository _customFoodRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<DeleteCustomFoodCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public DeleteCustomFoodCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        ICustomFoodRepository customFoodRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        ILogger<DeleteCustomFoodCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _customFoodRepository = customFoodRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Handle(DeleteCustomFoodCommand request, CancellationToken cancellationToken)
    {
        var patientId = await ResolveCurrentPatientIdAsync(cancellationToken).ConfigureAwait(false);

        var customFood = await _customFoodRepository.FindByIdAsync(request.CustomFoodId, cancellationToken).ConfigureAwait(false)
            ?? throw new CustomFoodNotFoundException(request.CustomFoodId);

        if (customFood.PatientId != patientId)
        {
            throw new PatientResourceAccessException();
        }

        if (await _customFoodRepository.IsReferencedByMealItemsAsync(customFood.Id, cancellationToken).ConfigureAwait(false))
        {
            throw new CustomFoodInUseException(customFood.Id);
        }

        _customFoodRepository.Remove(customFood);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            AuditActionType.Delete, nameof(CustomFood), customFood.Id,
            oldValuesHash: null, newValuesHash: null, additionalContext: null, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Custom food {CustomFoodId} deleted.", customFood.Id);
    }

    private async Task<Guid> ResolveCurrentPatientIdAsync(CancellationToken ct)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), ct).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var patientRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Patient, ct).ConfigureAwait(false);
        if (user.RoleId != patientRoleId)
        {
            throw new UnauthorizedAccessException("Solo un paciente puede eliminar alimentos personalizados.");
        }

        return user.Id;
    }
}
