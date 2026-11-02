using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.Services;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetIbsSssCycleForNutritionist;

/// <summary>
/// Handler de <see cref="GetIbsSssCycleForNutritionistQuery"/>. Toma la agenda más reciente del paciente
/// y registra la lectura en la bitácora.
/// </summary>
public sealed class GetIbsSssCycleForNutritionistQueryHandler
    : IRequestHandler<GetIbsSssCycleForNutritionistQuery, IbsSssCycleStatusDto>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IIbsSssAssessmentScheduleRepository _scheduleRepository;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetIbsSssCycleForNutritionistQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        IIbsSssAssessmentScheduleRepository scheduleRepository,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
        _scheduleRepository = scheduleRepository;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<IbsSssCycleStatusDto> Handle(
        GetIbsSssCycleForNutritionistQuery request,
        CancellationToken cancellationToken)
    {
        await NutritionistPatientRecordAccess
            .EnsureActiveAssignmentAsync(
                _currentUserService, _userRepository, _nutritionistPatientRepository, request.PatientUserId, cancellationToken)
            .ConfigureAwait(false);

        var schedule = await _scheduleRepository
            .FindLatestByPatientAsync(request.PatientUserId, cancellationToken)
            .ConfigureAwait(false);

        await NutritionistPatientRecordAccess
            .RecordViewAsync(_auditLogger, _unitOfWork, request.PatientUserId, "ibs_sss_cycle", cancellationToken)
            .ConfigureAwait(false);

        if (schedule is null)
        {
            return new IbsSssCycleStatusDto(IbsSssCycleStatus.None, null, null, null);
        }

        var status = schedule.Completed
            ? IbsSssCycleStatus.Completed
            : schedule.Missed ? IbsSssCycleStatus.Missed : IbsSssCycleStatus.Pending;

        return new IbsSssCycleStatusDto(status, schedule.DueDate, schedule.ReminderSentAt, schedule.CompletedAt);
    }
}
