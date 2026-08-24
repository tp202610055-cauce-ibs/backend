using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using Cauce.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.ClinicalRegistry.UseCases.CreateClinicalNote;

/// <summary>
/// Handler de la creación de una nota clínica. Verifica que la comida o el síntoma
/// asociado pertenezca al paciente autenticado; registra el evento en auditoría. No
/// registra el contenido de la nota en ningún log.
/// </summary>
public sealed class CreateClinicalNoteCommandHandler : IRequestHandler<CreateClinicalNoteCommand, CreateClinicalNoteResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IClinicalNoteRepository _clinicalNoteRepository;
    private readonly IMealRepository _mealRepository;
    private readonly ISymptomRepository _symptomRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<CreateClinicalNoteCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public CreateClinicalNoteCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IClinicalNoteRepository clinicalNoteRepository,
        IMealRepository mealRepository,
        ISymptomRepository symptomRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        ILogger<CreateClinicalNoteCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _clinicalNoteRepository = clinicalNoteRepository;
        _mealRepository = mealRepository;
        _symptomRepository = symptomRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateClinicalNoteResult> Handle(CreateClinicalNoteCommand request, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var patientId = await ResolveCurrentPatientIdAsync(cancellationToken).ConfigureAwait(false);

        await EnsureAssociationOwnedByPatientAsync(request, patientId, cancellationToken).ConfigureAwait(false);

        var note = ClinicalNote.Attach(Guid.NewGuid(), patientId, request.MealId, request.SymptomId, request.Content, utcNow);

        await _clinicalNoteRepository.AddAsync(note, cancellationToken).ConfigureAwait(false);

        // Auditoría explícita ANTES del SaveChanges: clinical_notes no tiene trigger; una sola
        // transacción persiste la nota y la bitácora de forma atómica (DEC-B5-01 capa 3, acta A8).
        await _auditLogger.LogAsync(
            AuditActionType.Create, nameof(ClinicalNote), note.Id,
            oldValuesHash: null, newValuesHash: null, additionalContext: null, cancellationToken: cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Clinical note {NoteId} created.", note.Id);
        return new CreateClinicalNoteResult(note.Id);
    }

    private async Task EnsureAssociationOwnedByPatientAsync(CreateClinicalNoteCommand request, Guid patientId, CancellationToken ct)
    {
        if (request.MealId.HasValue)
        {
            var meal = await _mealRepository.FindByIdAsync(request.MealId.Value, ct).ConfigureAwait(false)
                ?? throw new MealNotFoundException(request.MealId.Value);
            if (meal.PatientId != patientId)
            {
                throw new PatientResourceAccessException();
            }
        }
        else if (request.SymptomId.HasValue)
        {
            var symptom = await _symptomRepository.FindByIdAsync(request.SymptomId.Value, ct).ConfigureAwait(false)
                ?? throw new SymptomNotFoundException(request.SymptomId.Value);
            if (symptom.PatientId != patientId)
            {
                throw new PatientResourceAccessException();
            }
        }
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
            throw new UnauthorizedAccessException("Solo un paciente puede crear notas clínicas.");
        }

        return user.Id;
    }
}
