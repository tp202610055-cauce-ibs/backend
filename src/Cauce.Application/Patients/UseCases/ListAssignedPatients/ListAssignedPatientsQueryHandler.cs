using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.Dtos;
using Cauce.Application.Patients.Services;
using Cauce.Domain.Identity;
using MediatR;

namespace Cauce.Application.Patients.UseCases.ListAssignedPatients;

/// <summary>
/// Handler del panel de triaje (US18): lista los pacientes activos asignados al
/// nutricionista autenticado y los ordena por prioridad con <see cref="AssignedPatientTriage"/>.
/// </summary>
public sealed class ListAssignedPatientsQueryHandler : IRequestHandler<ListAssignedPatientsQuery, IReadOnlyList<AssignedPatientSummary>>
{
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

        return AssignedPatientTriage.Rank(rows, utcNow);
    }
}
