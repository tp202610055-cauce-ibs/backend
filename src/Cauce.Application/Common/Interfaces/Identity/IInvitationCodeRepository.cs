using Cauce.Domain.Identity;

namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Repositorio del agregado <see cref="InvitationCode"/>.
/// </summary>
public interface IInvitationCodeRepository
{
    /// <summary>
    /// Busca un código de invitación por su valor.
    /// </summary>
    /// <param name="code">Valor del código.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El código o <see langword="null"/> si no existe.</returns>
    Task<InvitationCode?> FindByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Busca el código de invitación que fue consumido por un paciente específico.
    /// </summary>
    /// <param name="patientId">Identificador local del paciente.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El código consumido, o <see langword="null"/> si el paciente no usó ninguno.</returns>
    Task<InvitationCode?> FindByUsedByPatientIdAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Agrega un nuevo código de invitación al contexto de persistencia.
    /// </summary>
    /// <param name="invitation">Código a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(InvitationCode invitation, CancellationToken ct = default);
}
