using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.Mapping;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetClinicalNotes;

/// <summary>
/// Handler de la consulta de notas clínicas del paciente autenticado.
/// </summary>
public sealed class GetClinicalNotesQueryHandler : IRequestHandler<GetClinicalNotesQuery, IReadOnlyList<ClinicalNoteSummary>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IClinicalNoteRepository _clinicalNoteRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetClinicalNotesQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IClinicalNoteRepository clinicalNoteRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _clinicalNoteRepository = clinicalNoteRepository;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClinicalNoteSummary>> Handle(GetClinicalNotesQuery request, CancellationToken cancellationToken)
    {
        var patientId = await ResolveCurrentPatientIdAsync(cancellationToken).ConfigureAwait(false);
        var notes = await _clinicalNoteRepository
            .ListByPatientInRangeAsync(patientId, request.From, request.To, cancellationToken)
            .ConfigureAwait(false);

        return notes.Select(ClinicalRegistryMappings.ToSummary).ToList();
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
            throw new UnauthorizedAccessException("Solo un paciente puede consultar sus notas clínicas.");
        }

        return user.Id;
    }
}
