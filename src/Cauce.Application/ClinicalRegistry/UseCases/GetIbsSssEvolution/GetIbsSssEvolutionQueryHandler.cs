using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.Mapping;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetIbsSssEvolution;

/// <summary>
/// Handler de la consulta de evolución IBS-SSS. Calcula la diferencia de cada evaluación
/// respecto de la línea base (negativa indica mejoría).
/// </summary>
public sealed class GetIbsSssEvolutionQueryHandler : IRequestHandler<GetIbsSssEvolutionQuery, IReadOnlyList<IbsSssEvolutionEntry>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IIbsSssAssessmentRepository _assessmentRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetIbsSssEvolutionQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IIbsSssAssessmentRepository assessmentRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _assessmentRepository = assessmentRepository;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IbsSssEvolutionEntry>> Handle(GetIbsSssEvolutionQuery request, CancellationToken cancellationToken)
    {
        var patientId = await ResolveCurrentPatientIdAsync(cancellationToken).ConfigureAwait(false);
        var assessments = await _assessmentRepository.ListByPatientAsync(patientId, cancellationToken).ConfigureAwait(false);

        var baseline = assessments.FirstOrDefault(a => a.AssessmentType == AssessmentType.Baseline);

        return assessments
            .Select(assessment => new IbsSssEvolutionEntry(
                ClinicalRegistryMappings.ToSummary(assessment),
                ComputeDelta(assessment, baseline)))
            .ToList();
    }

    private static int? ComputeDelta(IbsSssAssessment assessment, IbsSssAssessment? baseline)
    {
        if (baseline is null || assessment.AssessmentType == AssessmentType.Baseline)
        {
            return null;
        }

        return assessment.CompareTotalScoreTo(baseline);
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
            throw new UnauthorizedAccessException("Solo un paciente puede consultar su evolución IBS-SSS.");
        }

        return user.Id;
    }
}
