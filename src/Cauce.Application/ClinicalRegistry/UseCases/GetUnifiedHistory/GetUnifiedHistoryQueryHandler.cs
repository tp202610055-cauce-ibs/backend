using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.Services;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetUnifiedHistory;

/// <summary>
/// Handler del historial unificado. Combina comidas, síntomas y notas clínicas del rango
/// y los ordena cronológicamente de forma descendente. Cada fuente está acotada para
/// evitar respuestas excesivas.
/// </summary>
public sealed class GetUnifiedHistoryQueryHandler : IRequestHandler<GetUnifiedHistoryQuery, IReadOnlyList<HistoryEvent>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IMealRepository _mealRepository;
    private readonly ISymptomRepository _symptomRepository;
    private readonly IClinicalNoteRepository _clinicalNoteRepository;
    private readonly IMealFodmapResolver _fodmapResolver;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetUnifiedHistoryQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IMealRepository mealRepository,
        ISymptomRepository symptomRepository,
        IClinicalNoteRepository clinicalNoteRepository,
        IMealFodmapResolver fodmapResolver)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _mealRepository = mealRepository;
        _symptomRepository = symptomRepository;
        _clinicalNoteRepository = clinicalNoteRepository;
        _fodmapResolver = fodmapResolver;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HistoryEvent>> Handle(GetUnifiedHistoryQuery request, CancellationToken cancellationToken)
    {
        var patientId = await ResolveCurrentPatientIdAsync(cancellationToken).ConfigureAwait(false);

        return await UnifiedHistoryReader
            .ReadAsync(
                patientId, request.From, request.To,
                _mealRepository, _symptomRepository, _clinicalNoteRepository, _fodmapResolver,
                cancellationToken)
            .ConfigureAwait(false);
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
            throw new UnauthorizedAccessException("Solo un paciente puede consultar su historial.");
        }

        return user.Id;
    }
}
