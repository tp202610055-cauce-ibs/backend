using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Identity.Events;
using Cauce.Domain.Patients;

namespace Cauce.Application.Patients.Services;

/// <summary>
/// Implementación de <see cref="IPatientNutritionistAssignmentService"/>. Reúne la lógica de
/// vinculación que antes vivía repartida entre el handler de registro y el de creación de perfil
/// (acta A41), sin alterar lo que cada uno hace.
/// </summary>
public sealed class PatientNutritionistAssignmentService : IPatientNutritionistAssignmentService
{
    private readonly IInvitationCodeRepository _invitationCodeRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IOutboxWriter _outboxWriter;

    /// <summary>
    /// Inicializa el servicio con sus dependencias.
    /// </summary>
    /// <param name="invitationCodeRepository">Repositorio de códigos de invitación.</param>
    /// <param name="nutritionistPatientRepository">Repositorio de asignaciones.</param>
    /// <param name="outboxWriter">Escritor del outbox transaccional.</param>
    public PatientNutritionistAssignmentService(
        IInvitationCodeRepository invitationCodeRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        IOutboxWriter outboxWriter)
    {
        _invitationCodeRepository = invitationCodeRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
        _outboxWriter = outboxWriter;
    }

    /// <inheritdoc />
    public async Task ConsumeInvitationAsync(
        Guid patientId,
        InvitationCode invitation,
        DateTime utcNow,
        LinkContext context = LinkContext.RegistrationLink,
        CancellationToken ct = default)
    {
        invitation.MarkAsUsed(patientId, utcNow);

        // US20 CA01: notificar al nutricionista del nuevo paciente vinculado, de forma asíncrona vía
        // outbox (patrón DEC-B5-04).
        await _outboxWriter.PublishAsync(
            patientId,
            nameof(User),
            new PatientLinkedToNutritionistEvent(patientId, invitation.NutritionistId, invitation.Id, utcNow, context),
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<(bool Assigned, Guid? AssignmentId)> EstablishAssignmentAsync(
        Guid patientId,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        var invitation = await _invitationCodeRepository
            .FindByUsedByPatientIdAsync(patientId, ct)
            .ConfigureAwait(false);
        if (invitation is null)
        {
            return (false, null);
        }

        return await EstablishAssignmentAsync(patientId, invitation, utcNow, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<(bool Assigned, Guid? AssignmentId)> EstablishAssignmentAsync(
        Guid patientId,
        InvitationCode invitation,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        if (await _nutritionistPatientRepository
            .ActiveAssignmentExistsAsync(invitation.NutritionistId, patientId, ct)
            .ConfigureAwait(false))
        {
            return (false, null);
        }

        var assignment = NutritionistPatient.Establish(
            Guid.NewGuid(),
            invitation.NutritionistId,
            patientId,
            invitation.Id,
            utcNow);
        await _nutritionistPatientRepository.AddAsync(assignment, ct).ConfigureAwait(false);
        return (true, assignment.Id);
    }
}
