using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.Dtos;
using Cauce.Application.Patients.Mapping;
using MediatR;

namespace Cauce.Application.Patients.UseCases.ListPatientAllergies;

/// <summary>
/// Handler que devuelve las alergias declaradas por el paciente autenticado,
/// resueltas con el nombre del catálogo.
/// </summary>
public sealed class ListPatientAllergiesQueryHandler : IRequestHandler<ListPatientAllergiesQuery, IReadOnlyList<PatientAllergySummary>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IAllergyRepository _allergyRepository;
    private readonly IPatientAllergyRepository _patientAllergyRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public ListPatientAllergiesQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IAllergyRepository allergyRepository,
        IPatientAllergyRepository patientAllergyRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _allergyRepository = allergyRepository;
        _patientAllergyRepository = patientAllergyRepository;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PatientAllergySummary>> Handle(ListPatientAllergiesQuery request, CancellationToken cancellationToken)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), cancellationToken).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var declarations = await _patientAllergyRepository.ListByPatientAsync(user.Id, cancellationToken).ConfigureAwait(false);
        return await PatientAllergyMapper.MapAsync(declarations, _allergyRepository, cancellationToken).ConfigureAwait(false);
    }
}
