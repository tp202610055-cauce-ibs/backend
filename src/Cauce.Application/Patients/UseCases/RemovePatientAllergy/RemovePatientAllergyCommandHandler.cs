using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Patients.UseCases.RemovePatientAllergy;

/// <summary>
/// Handler de la eliminación de una declaración de alergia. Verifica que la
/// declaración pertenezca al paciente autenticado antes de eliminarla.
/// </summary>
public sealed class RemovePatientAllergyCommandHandler : IRequestHandler<RemovePatientAllergyCommand>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IPatientAllergyRepository _patientAllergyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RemovePatientAllergyCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public RemovePatientAllergyCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IPatientAllergyRepository patientAllergyRepository,
        IUnitOfWork unitOfWork,
        ILogger<RemovePatientAllergyCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _patientAllergyRepository = patientAllergyRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Handle(RemovePatientAllergyCommand request, CancellationToken cancellationToken)
    {
        var user = await ResolvePatientAsync(cancellationToken).ConfigureAwait(false);

        var declaration = await _patientAllergyRepository.FindByIdAsync(request.PatientAllergyId, cancellationToken).ConfigureAwait(false)
            ?? throw new AllergyNotFoundException();

        if (declaration.PatientId != user.Id)
        {
            throw new UnauthorizedAccessException("La declaración de alergia no pertenece al paciente autenticado.");
        }

        _patientAllergyRepository.Remove(declaration);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // La auditoría de patient_allergies la realiza el trigger de PostgreSQL (DEC-B5-03).

        _logger.LogInformation("Patient allergy {PatientAllergyId} removed.", declaration.Id);
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
            throw new UnauthorizedAccessException("Solo un paciente puede eliminar sus alergias.");
        }

        return user;
    }
}
