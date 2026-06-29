using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.Mapping;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetLatestIbsSssAssessment;

/// <summary>
/// Handler de la consulta de la evaluación IBS-SSS más reciente del paciente autenticado.
/// </summary>
public sealed class GetLatestIbsSssAssessmentQueryHandler : IRequestHandler<GetLatestIbsSssAssessmentQuery, IbsSssAssessmentSummary?>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IIbsSssAssessmentRepository _assessmentRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetLatestIbsSssAssessmentQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IIbsSssAssessmentRepository assessmentRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _assessmentRepository = assessmentRepository;
    }

    /// <inheritdoc />
    public async Task<IbsSssAssessmentSummary?> Handle(GetLatestIbsSssAssessmentQuery request, CancellationToken cancellationToken)
    {
        var patientId = await ResolveCurrentPatientIdAsync(cancellationToken).ConfigureAwait(false);
        var latest = await _assessmentRepository.FindLatestByPatientAsync(patientId, cancellationToken).ConfigureAwait(false);
        return latest is null ? null : ClinicalRegistryMappings.ToSummary(latest);
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
            throw new UnauthorizedAccessException("Solo un paciente puede consultar su evaluación IBS-SSS.");
        }

        return user.Id;
    }
}
