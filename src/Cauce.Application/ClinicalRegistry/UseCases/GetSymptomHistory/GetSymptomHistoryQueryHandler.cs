using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.Mapping;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Models;
using Cauce.Domain.Identity;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetSymptomHistory;

/// <summary>
/// Handler de la consulta paginada del historial de síntomas del paciente autenticado.
/// </summary>
public sealed class GetSymptomHistoryQueryHandler : IRequestHandler<GetSymptomHistoryQuery, PagedResult<SymptomHistoryItem>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly ISymptomRepository _symptomRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetSymptomHistoryQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        ISymptomRepository symptomRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _symptomRepository = symptomRepository;
    }

    /// <inheritdoc />
    public async Task<PagedResult<SymptomHistoryItem>> Handle(GetSymptomHistoryQuery request, CancellationToken cancellationToken)
    {
        var patientId = await ResolveCurrentPatientIdAsync(cancellationToken).ConfigureAwait(false);
        var skip = (request.Page - 1) * request.PageSize;

        var symptoms = await _symptomRepository
            .ListByPatientInRangeAsync(patientId, request.From, request.To, skip, request.PageSize, cancellationToken)
            .ConfigureAwait(false);

        var total = await _symptomRepository
            .CountByPatientInRangeAsync(patientId, request.From, request.To, cancellationToken)
            .ConfigureAwait(false);

        var items = symptoms.Select(ClinicalRegistryMappings.ToHistoryItem).ToList();
        return new PagedResult<SymptomHistoryItem>(items, request.Page, request.PageSize, total);
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
            throw new UnauthorizedAccessException("Solo un paciente puede consultar su historial de síntomas.");
        }

        return user.Id;
    }
}
