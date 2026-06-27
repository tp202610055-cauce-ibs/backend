using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.Dtos;
using Cauce.Domain.Identity;
using MediatR;

namespace Cauce.Application.Patients.UseCases.ListAssignedPatients;

/// <summary>
/// Handler que lista los pacientes activos asignados al nutricionista autenticado.
/// La resolución de nombres y perfiles se hace con una única consulta con joins.
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

        return await _nutritionistPatientRepository
            .ListAssignedPatientSummariesAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);
    }
}
