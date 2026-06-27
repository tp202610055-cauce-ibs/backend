using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.Dtos;
using MediatR;

namespace Cauce.Application.Patients.UseCases.GetNutritionistAssignmentForPatient;

/// <summary>
/// Handler de la consulta interna de asignación activa de un paciente.
/// </summary>
public sealed class GetNutritionistAssignmentForPatientQueryHandler
    : IRequestHandler<GetNutritionistAssignmentForPatientQuery, NutritionistAssignmentSummary?>
{
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IUserRepository _userRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetNutritionistAssignmentForPatientQueryHandler(
        INutritionistPatientRepository nutritionistPatientRepository,
        IUserRepository userRepository)
    {
        _nutritionistPatientRepository = nutritionistPatientRepository;
        _userRepository = userRepository;
    }

    /// <inheritdoc />
    public async Task<NutritionistAssignmentSummary?> Handle(GetNutritionistAssignmentForPatientQuery request, CancellationToken cancellationToken)
    {
        var assignment = await _nutritionistPatientRepository
            .FindActiveByPatientAsync(request.PatientUserId, cancellationToken)
            .ConfigureAwait(false);
        if (assignment is null)
        {
            return null;
        }

        var nutritionist = await _userRepository.FindByIdAsync(assignment.NutritionistId, cancellationToken).ConfigureAwait(false);
        return new NutritionistAssignmentSummary(
            assignment.Id,
            assignment.NutritionistId,
            nutritionist?.FullName ?? string.Empty,
            assignment.AssignedAt);
    }
}
