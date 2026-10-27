using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.Services;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.Services;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetPatientHistoryForNutritionist;

/// <summary>
/// Handler de <see cref="GetPatientHistoryForNutritionistQuery"/>. Devuelve el mismo historial que ve
/// el paciente y registra la lectura en la bitácora.
/// </summary>
public sealed class GetPatientHistoryForNutritionistQueryHandler
    : IRequestHandler<GetPatientHistoryForNutritionistQuery, IReadOnlyList<HistoryEvent>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IMealRepository _mealRepository;
    private readonly ISymptomRepository _symptomRepository;
    private readonly IClinicalNoteRepository _clinicalNoteRepository;
    private readonly IMealFodmapResolver _fodmapResolver;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetPatientHistoryForNutritionistQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        IMealRepository mealRepository,
        ISymptomRepository symptomRepository,
        IClinicalNoteRepository clinicalNoteRepository,
        IMealFodmapResolver fodmapResolver,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
        _mealRepository = mealRepository;
        _symptomRepository = symptomRepository;
        _clinicalNoteRepository = clinicalNoteRepository;
        _fodmapResolver = fodmapResolver;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HistoryEvent>> Handle(
        GetPatientHistoryForNutritionistQuery request,
        CancellationToken cancellationToken)
    {
        await NutritionistPatientRecordAccess
            .EnsureActiveAssignmentAsync(
                _currentUserService, _userRepository, _nutritionistPatientRepository, request.PatientUserId, cancellationToken)
            .ConfigureAwait(false);

        var events = await UnifiedHistoryReader
            .ReadAsync(
                request.PatientUserId, request.From, request.To,
                _mealRepository, _symptomRepository, _clinicalNoteRepository, _fodmapResolver,
                cancellationToken)
            .ConfigureAwait(false);

        await NutritionistPatientRecordAccess
            .RecordViewAsync(_auditLogger, _unitOfWork, request.PatientUserId, "history", cancellationToken)
            .ConfigureAwait(false);

        return events;
    }
}
