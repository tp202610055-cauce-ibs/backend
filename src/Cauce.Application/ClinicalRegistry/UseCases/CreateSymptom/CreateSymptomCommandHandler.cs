using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.ClinicalRegistry.UseCases.CreateSymptom;

/// <summary>
/// Handler del registro de un síntoma. Calcula del lado servidor la asociación con la
/// comida más reciente del paciente cuya creación en el dispositivo cae dentro de la
/// ventana de 4 horas anterior al síntoma (DEC-B3-06). No registra el evento en
/// auditoría por su alto volumen.
/// </summary>
public sealed class CreateSymptomCommandHandler : IRequestHandler<CreateSymptomCommand, CreateSymptomResult>
{
    /// <summary>
    /// Ventana temporal canónica para correlacionar un síntoma con la comida previa.
    /// Fundamentada en Monash University (2019) y Ford et al. (2024, <c>Gut</c>).
    /// </summary>
    private static readonly TimeSpan AssociationWindow = TimeSpan.FromHours(4);

    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly ISymptomRepository _symptomRepository;
    private readonly IMealRepository _mealRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateSymptomCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public CreateSymptomCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        ISymptomRepository symptomRepository,
        IMealRepository mealRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateSymptomCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _symptomRepository = symptomRepository;
        _mealRepository = mealRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateSymptomResult> Handle(CreateSymptomCommand request, CancellationToken cancellationToken)
    {
        var serverUtcNow = DateTime.UtcNow;
        var patientId = await ResolveCurrentPatientIdAsync(cancellationToken).ConfigureAwait(false);

        var symptom = Symptom.Report(
            Guid.NewGuid(),
            request.ClientGuid,
            patientId,
            request.SymptomType,
            request.Intensity,
            request.OccurredAt,
            request.ClientCreatedAt,
            serverUtcNow);

        var windowStart = request.ClientCreatedAt - AssociationWindow;
        var latestMeal = await _mealRepository
            .FindLatestInWindowAsync(patientId, windowStart, request.ClientCreatedAt, cancellationToken)
            .ConfigureAwait(false);

        if (latestMeal is not null)
        {
            symptom.AssociateWithMeal(latestMeal.Id, serverUtcNow);
        }

        await _symptomRepository.AddAsync(symptom, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Symptom {SymptomId} registered (meal association: {HasAssociation}).",
            symptom.Id,
            symptom.HasMealAssociation);

        return new CreateSymptomResult(symptom.Id, symptom.AssociatedMealId, symptom.HasMealAssociation);
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
            throw new UnauthorizedAccessException("Solo un paciente puede registrar síntomas.");
        }

        return user.Id;
    }
}
