using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Patients.UseCases.DeclarePatientAllergy;

/// <summary>
/// Handler de la declaración de una alergia por parte del paciente autenticado.
/// Valida que la alergia exista, esté activa y no haya sido declarada antes.
/// </summary>
public sealed class DeclarePatientAllergyCommandHandler : IRequestHandler<DeclarePatientAllergyCommand, DeclarePatientAllergyResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IAllergyRepository _allergyRepository;
    private readonly IPatientAllergyRepository _patientAllergyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<DeclarePatientAllergyCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public DeclarePatientAllergyCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IAllergyRepository allergyRepository,
        IPatientAllergyRepository patientAllergyRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        ILogger<DeclarePatientAllergyCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _allergyRepository = allergyRepository;
        _patientAllergyRepository = patientAllergyRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DeclarePatientAllergyResult> Handle(DeclarePatientAllergyCommand request, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var user = await ResolvePatientAsync(cancellationToken).ConfigureAwait(false);

        var allergy = await _allergyRepository.FindByIdAsync(request.AllergyId, cancellationToken).ConfigureAwait(false);
        if (allergy is null || !allergy.IsActive)
        {
            throw new AllergyNotFoundException();
        }

        if (await _patientAllergyRepository.ExistsAsync(user.Id, request.AllergyId, cancellationToken).ConfigureAwait(false))
        {
            throw new DuplicatePatientAllergyException();
        }

        var declaration = PatientAllergy.Declare(Guid.NewGuid(), user.Id, request.AllergyId, request.Severity, request.Notes, utcNow);
        await _patientAllergyRepository.AddAsync(declaration, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            AuditActionType.Create, nameof(PatientAllergy), declaration.Id,
            oldValuesHash: null, newValuesHash: null, additionalContext: null, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Patient allergy {PatientAllergyId} declared.", declaration.Id);

        return new DeclarePatientAllergyResult(declaration.Id);
    }

    private async Task<User> ResolvePatientAsync(CancellationToken ct)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), ct).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var patientRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Patient, ct).ConfigureAwait(false);
        if (user.RoleId != patientRoleId)
        {
            throw new UnauthorizedAccessException("Solo un paciente puede declarar alergias.");
        }

        return user;
    }
}
