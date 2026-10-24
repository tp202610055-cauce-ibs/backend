using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients.Enums;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Infrastructure.Qa.Identity;

/// <summary>
/// Retira una cuenta local de un rol sin borrarla: la deja inactiva y con el correo liberado. No guarda.
/// </summary>
public interface IQaAccountRetirer
{
    /// <summary>
    /// Nombre del rol (<see cref="UserRoles"/>) que este retirador atiende.
    /// </summary>
    string RoleName { get; }

    /// <summary>
    /// Retira la cuenta en el contexto, sin guardar.
    /// </summary>
    Task RetireAsync(User user, DateTime utcNow, CancellationToken ct);
}

/// <summary>
/// Resultado de retirar una cuenta por correo.
/// </summary>
public enum QaRetireOutcome
{
    Absent,
    Retired,
    UnsupportedRole
}

/// <summary>
/// Saca de circulación la cuenta de un correo: la borra en Keycloak y retira la cuenta local con el retirador de su rol.
/// La cuenta local nunca se borra físicamente; sus datos clínicos quedan, sin vínculos activos.
/// </summary>
public sealed class QaAccountRetirement
{
    private readonly CauceDbContext _db;
    private readonly IKeycloakAdminClient _keycloak;
    private readonly IReadOnlyList<IQaAccountRetirer> _retirers;

    /// <summary>
    /// Inicializa el retiro con los retiradores de rol de este ensamblado.
    /// </summary>
    public QaAccountRetirement(CauceDbContext db, IKeycloakAdminClient keycloak, IServiceProvider services)
    {
        _db = db;
        _keycloak = keycloak;
        _retirers = typeof(IQaAccountRetirer).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IQaAccountRetirer).IsAssignableFrom(type))
            .Select(type => (IQaAccountRetirer)ActivatorUtilities.CreateInstance(services, type))
            .ToList();
    }

    /// <summary>
    /// Indica si hay retirador para el rol.
    /// </summary>
    public bool Supports(string roleName) => _retirers.Any(retirer => retirer.RoleName == roleName);

    /// <summary>
    /// Retira la cuenta del correo, si existe, y guarda. Una cuenta sin retirador para su rol no se toca.
    /// </summary>
    public async Task<QaRetireOutcome> RetireAsync(string email, DateTime utcNow, CancellationToken ct)
    {
        var normalized = EmailNormalization.Normalize(email);
        var user = await _db.Users.FirstOrDefaultAsync(candidate => candidate.Email == normalized, ct).ConfigureAwait(false);
        var outcome = QaRetireOutcome.Absent;

        if (user is not null)
        {
            var roleName = await _db.UserRoles.Where(role => role.RoleId == user.RoleId).Select(role => role.RoleName)
                .FirstAsync(ct).ConfigureAwait(false);
            var retirer = _retirers.FirstOrDefault(candidate => candidate.RoleName == roleName);
            if (retirer is null)
            {
                return QaRetireOutcome.UnsupportedRole;
            }

            await _keycloak.DeleteUserAsync(user.KeycloakId, ct).ConfigureAwait(false);
            await retirer.RetireAsync(user, utcNow, ct).ConfigureAwait(false);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            outcome = QaRetireOutcome.Retired;
        }

        // Una identidad de Keycloak sin cuenta local (alta fallida o baja que solo la deshabilitó) también se borra.
        var orphan = await _keycloak.FindByEmailAsync(normalized, ct).ConfigureAwait(false);
        if (orphan is not null)
        {
            await _keycloak.DeleteUserAsync(orphan.Id, ct).ConfigureAwait(false);
            outcome = QaRetireOutcome.Retired;
        }

        return outcome;
    }
}

/// <summary>
/// Retira un paciente con la misma anonimización de la baja de cuenta y desactiva sus vínculos, para que ninguna
/// lista del nutricionista lo muestre.
/// </summary>
public sealed class QaPatientRetirer : IQaAccountRetirer
{
    private readonly CauceDbContext _db;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// Inicializa el retirador con el contexto y la bitácora.
    /// </summary>
    public QaPatientRetirer(CauceDbContext db, IAuditLogger auditLogger)
    {
        _db = db;
        _auditLogger = auditLogger;
    }

    /// <inheritdoc />
    public string RoleName => UserRoles.Patient;

    /// <inheritdoc />
    public async Task RetireAsync(User user, DateTime utcNow, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.Anonymize(user.RoleId);

        var links = await _db.NutritionistPatients
            .Where(link => link.PatientId == user.Id && link.Status == AssignmentStatus.Active)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var link in links)
        {
            link.Deactivate(utcNow);
        }

        // users no tiene trigger: el retiro queda en la bitácora con una fila explícita.
        await _auditLogger.LogAsync(
            AuditActionType.Delete,
            nameof(User),
            user.Id,
            oldValuesHash: null,
            newValuesHash: null,
            additionalContext: JsonSerializer.Serialize(new { operation = "anonymization", actor = "qa_command" }),
            cancellationToken: ct).ConfigureAwait(false);
    }
}
