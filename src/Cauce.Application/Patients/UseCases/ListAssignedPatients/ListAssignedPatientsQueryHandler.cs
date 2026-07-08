using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.Dtos;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients.Enums;
using MediatR;

namespace Cauce.Application.Patients.UseCases.ListAssignedPatients;

/// <summary>
/// Handler del panel de triaje (US18): lista los pacientes activos asignados al
/// nutricionista autenticado, calcula el nivel de prioridad de atención de cada uno a
/// partir de las métricas de la consulta y los ordena de mayor a menor urgencia.
/// </summary>
public sealed class ListAssignedPatientsQueryHandler : IRequestHandler<ListAssignedPatientsQuery, IReadOnlyList<AssignedPatientSummary>>
{
    private const int ProlongedInactivityDays = 7;


    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public ListAssignedPatientsQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        INutritionistPatientRepository nutritionistPatientRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssignedPatientSummary>> Handle(ListAssignedPatientsQuery request, CancellationToken cancellationToken)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), cancellationToken).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var nutritionistRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, cancellationToken).ConfigureAwait(false);
        if (user.RoleId != nutritionistRoleId)
        {
            throw new UnauthorizedAccessException("Solo un nutricionista puede listar sus pacientes asignados.");
        }

        var utcNow = DateTime.UtcNow;
        var rows = await _nutritionistPatientRepository
            .ListAssignedPatientTriageRowsAsync(user.Id, utcNow, cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(row => ToSummary(row, utcNow))
            .OrderByDescending(summary => summary.PriorityLevel)
            .ThenByDescending(summary => summary.LatestIbsSssScore ?? int.MinValue)
            .ThenBy(summary => summary.AssignedAt)
            .ToList();
    }

    private static AssignedPatientSummary ToSummary(AssignedPatientTriageRow row, DateTime utcNow)
    {
        var lastActivityAt = MaxDate(row.LastMealAt, row.LastSymptomAt, row.LastAssessmentAt);
        var priority = ComputePriority(row, lastActivityAt, utcNow);

        return new AssignedPatientSummary(
            row.PatientUserId,
            row.PatientFullName,
            row.AssignmentId,
            row.AssignedAt,
            row.ProfileCompleted,
            row.IbsSubtype,
            row.LatestIbsSssScore,
            lastActivityAt,
            row.PendingReviewOver24hCount,
            priority);
    }

    private static PriorityLevel ComputePriority(AssignedPatientTriageRow row, DateTime? lastActivityAt, DateTime utcNow)
    {
        if (row.PendingReviewOver24hCount > 0 || row.LatestSeverity == SeverityCategory.Severe)
        {
            return PriorityLevel.High;
        }

        var isInactive = lastActivityAt is not null && lastActivityAt < utcNow.AddDays(-ProlongedInactivityDays);
        if (row.LatestSeverity == SeverityCategory.Moderate || isInactive)
        {
            return PriorityLevel.Medium;
        }

        if (row.LatestIbsSssScore is not null || lastActivityAt is not null)
        {
            return PriorityLevel.Low;
        }

        return PriorityLevel.None;
    }

    private static DateTime? MaxDate(params DateTime?[] dates)
    {
        DateTime? max = null;
        foreach (var date in dates)
        {
            if (date is not null && (max is null || date > max))
            {
                max = date;
            }
        }

        return max;
    }
}
