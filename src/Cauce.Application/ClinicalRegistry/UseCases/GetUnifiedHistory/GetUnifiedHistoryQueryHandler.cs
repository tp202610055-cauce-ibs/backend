using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.Mapping;
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
    private const int MaxPerSource = 500;

    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IMealRepository _mealRepository;
    private readonly ISymptomRepository _symptomRepository;
    private readonly IClinicalNoteRepository _clinicalNoteRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetUnifiedHistoryQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IMealRepository mealRepository,
        ISymptomRepository symptomRepository,
        IClinicalNoteRepository clinicalNoteRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _mealRepository = mealRepository;
        _symptomRepository = symptomRepository;
        _clinicalNoteRepository = clinicalNoteRepository;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HistoryEvent>> Handle(GetUnifiedHistoryQuery request, CancellationToken cancellationToken)
    {
        var patientId = await ResolveCurrentPatientIdAsync(cancellationToken).ConfigureAwait(false);

        var meals = await _mealRepository
            .ListByPatientInRangeAsync(patientId, request.From, request.To, 0, MaxPerSource, cancellationToken)
            .ConfigureAwait(false);

        var symptoms = await _symptomRepository
            .ListByPatientInRangeAsync(patientId, request.From, request.To, 0, MaxPerSource, cancellationToken)
            .ConfigureAwait(false);

        var notes = await _clinicalNoteRepository
            .ListByPatientInRangeAsync(patientId, request.From, request.To, cancellationToken)
            .ConfigureAwait(false);

        var events = new List<HistoryEvent>(meals.Count + symptoms.Count + notes.Count);
        events.AddRange(meals.Select(meal => new MealHistoryEvent(meal.ConsumedAt, ClinicalRegistryMappings.ToHistoryItem(meal))));
        events.AddRange(symptoms.Select(symptom => new SymptomHistoryEvent(symptom.OccurredAt, ClinicalRegistryMappings.ToHistoryItem(symptom))));
        events.AddRange(notes.Select(note => new ClinicalNoteHistoryEvent(note.CreatedAt, ClinicalRegistryMappings.ToSummary(note))));

        return events
            .OrderByDescending(historyEvent => historyEvent.OccurredAt)
            .ToList();
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
