using Cauce.Domain.Common;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Identity.Exceptions;

namespace Cauce.Domain.Identity;

/// <summary>
/// Cuenta de usuario del sistema. Es raíz de agregado. Las credenciales se
/// delegan íntegramente a Keycloak: esta entidad nunca almacena contraseñas ni
/// hashes. El vínculo con Keycloak es <see cref="KeycloakId"/>.
/// </summary>
public sealed class User : Entity, IAggregateRoot
{
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Identificador del usuario en Keycloak (subject del JWT).
    /// </summary>
    public string KeycloakId { get; private set; } = string.Empty;

    /// <summary>
    /// Correo electrónico único de la cuenta.
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// Nombre completo del usuario.
    /// </summary>
    public string FullName { get; private set; } = string.Empty;

    /// <summary>
    /// Clave foránea al catálogo de roles (<c>user_roles.role_id</c>).
    /// </summary>
    public int RoleId { get; private set; }

    /// <summary>
    /// Estado del ciclo de vida de la cuenta.
    /// </summary>
    public UserStatus Status { get; private set; }

    /// <summary>
    /// Indica si el correo electrónico fue verificado.
    /// </summary>
    public bool EmailVerified { get; private set; }

    /// <summary>
    /// Momento de creación de la cuenta, en UTC.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Momento de la última modificación, en UTC.
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Momento del último inicio de sesión exitoso, en UTC; <see langword="null"/>
    /// si nunca inició sesión.
    /// </summary>
    public DateTime? LastLoginAt { get; private set; }

    /// <summary>
    /// Número de intentos fallidos de inicio de sesión consecutivos.
    /// </summary>
    public int FailedLoginAttempts { get; private set; }

    /// <summary>
    /// Momento, en UTC, hasta el cual la cuenta está bloqueada; <see langword="null"/>
    /// si no está bloqueada.
    /// </summary>
    public DateTime? LockedUntil { get; private set; }

    /// <summary>
    /// Token de registro del dispositivo para notificaciones push (Firebase Cloud Messaging), o
    /// <see langword="null"/> si no se ha registrado. Lo provee la app móvil vía el endpoint
    /// <c>PUT /users/me/fcm-token</c>.
    /// </summary>
    public string? FcmToken { get; private set; }

    /// <summary>
    /// Indica si la cuenta pertenece a un paciente inscrito en el piloto clínico activo. Es un dato
    /// de estado de la cuenta (no clínico). En este release solo se activa manualmente por el equipo
    /// clínico Kaelín (DB directa o endpoint admin futuro); no hay mecanismo automático (acta A16).
    /// Cuando es <see langword="true"/>, la eliminación de la cuenta exige acuse explícito de la
    /// retención normativa (US26 CA02).
    /// </summary>
    public bool IsInActivePilot { get; private set; }

    private User()
    {
    }

    private User(Guid id, string keycloakId, string email, string fullName, int roleId, UserStatus status, bool emailVerified)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keycloakId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(roleId);

        KeycloakId = keycloakId;
        Email = email;
        FullName = fullName;
        RoleId = roleId;
        Status = status;
        EmailVerified = emailVerified;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>
    /// Crea una cuenta de paciente en estado pendiente de activación y con correo
    /// no verificado.
    /// </summary>
    /// <param name="id">Identificador de la cuenta.</param>
    /// <param name="keycloakId">Identificador del usuario en Keycloak.</param>
    /// <param name="email">Correo electrónico.</param>
    /// <param name="fullName">Nombre completo.</param>
    /// <param name="patientRoleId">Identificador del rol paciente en el catálogo.</param>
    /// <returns>La nueva cuenta de paciente.</returns>
    public static User CreatePatient(Guid id, string keycloakId, string email, string fullName, int patientRoleId)
    {
        return new User(id, keycloakId, email, fullName, patientRoleId, UserStatus.PendingActivation, emailVerified: false);
    }

    /// <summary>
    /// Crea una cuenta de nutricionista en estado activo y con correo verificado,
    /// ya que se provisiona administrativamente.
    /// </summary>
    /// <param name="id">Identificador de la cuenta.</param>
    /// <param name="keycloakId">Identificador del usuario en Keycloak.</param>
    /// <param name="email">Correo electrónico.</param>
    /// <param name="fullName">Nombre completo.</param>
    /// <param name="nutritionistRoleId">Identificador del rol nutricionista en el catálogo.</param>
    /// <returns>La nueva cuenta de nutricionista.</returns>
    public static User CreateNutritionist(Guid id, string keycloakId, string email, string fullName, int nutritionistRoleId)
    {
        return new User(id, keycloakId, email, fullName, nutritionistRoleId, UserStatus.Active, emailVerified: true);
    }

    /// <summary>
    /// Activa la cuenta. Solo es válido sobre cuentas pendientes con correo verificado.
    /// </summary>
    /// <exception cref="InvalidOperationException">Si la cuenta no está pendiente o el correo no está verificado.</exception>
    public void Activate()
    {
        if (Status != UserStatus.PendingActivation)
        {
            throw new InvalidOperationException("Solo las cuentas pendientes de activación pueden activarse.");
        }

        if (!EmailVerified)
        {
            throw new InvalidOperationException("No se puede activar una cuenta con el correo sin verificar.");
        }

        Status = UserStatus.Active;
        Touch();
    }

    /// <summary>
    /// Suspende la cuenta administrativamente.
    /// </summary>
    public void Suspend()
    {
        Status = UserStatus.Suspended;
        Touch();
    }

    /// <summary>
    /// Reactiva una cuenta suspendida o inactiva.
    /// </summary>
    /// <exception cref="InvalidOperationException">Si la cuenta no está suspendida ni inactiva.</exception>
    public void Reactivate()
    {
        if (Status is not (UserStatus.Suspended or UserStatus.Inactive))
        {
            throw new InvalidOperationException("Solo las cuentas suspendidas o inactivas pueden reactivarse.");
        }

        Status = UserStatus.Active;
        Touch();
    }

    /// <summary>
    /// Marca el correo como verificado. Si la cuenta estaba pendiente, la activa.
    /// </summary>
    public void VerifyEmail()
    {
        EmailVerified = true;
        if (Status == UserStatus.PendingActivation)
        {
            Status = UserStatus.Active;
        }

        Touch();
    }

    /// <summary>
    /// Registra un inicio de sesión exitoso: actualiza la fecha del último acceso
    /// y reinicia el contador de intentos fallidos y el bloqueo. No modifica
    /// <see cref="UpdatedAt"/>.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC del inicio de sesión.</param>
    public void RegisterSuccessfulLogin(DateTime utcNow)
    {
        LastLoginAt = utcNow;
        FailedLoginAttempts = 0;
        LockedUntil = null;
    }

    /// <summary>
    /// Registra un intento de inicio de sesión fallido. Al alcanzar el máximo de
    /// intentos consecutivos, bloquea la cuenta por 15 minutos.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC del intento.</param>
    public void RegisterFailedLogin(DateTime utcNow)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= MaxFailedLoginAttempts)
        {
            Lock(utcNow + LockDuration);
        }

        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Bloquea la cuenta hasta el momento indicado.
    /// </summary>
    /// <param name="until">Momento UTC hasta el cual la cuenta queda bloqueada.</param>
    /// <exception cref="ArgumentException">Si el momento de desbloqueo no es futuro.</exception>
    public void Lock(DateTime until)
    {
        if (until <= DateTime.UtcNow)
        {
            throw new ArgumentException("El momento de desbloqueo debe ser futuro.", nameof(until));
        }

        LockedUntil = until;
        Touch();
    }

    /// <summary>
    /// Desbloquea la cuenta y reinicia el contador de intentos fallidos.
    /// </summary>
    public void Unlock()
    {
        LockedUntil = null;
        FailedLoginAttempts = 0;
        Touch();
    }

    /// <summary>
    /// Actualiza el nombre completo de la cuenta.
    /// </summary>
    /// <param name="newFullName">Nuevo nombre completo.</param>
    /// <exception cref="ArgumentException">Si el nombre es nulo o vacío.</exception>
    public void UpdateFullName(string newFullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newFullName);
        FullName = newFullName;
        Touch();
    }

    /// <summary>
    /// Registra o actualiza el token de notificaciones push del dispositivo del usuario.
    /// </summary>
    /// <param name="fcmToken">Token de FCM, o <see langword="null"/> para desvincularlo.</param>
    public void RegisterFcmToken(string? fcmToken)
    {
        FcmToken = string.IsNullOrWhiteSpace(fcmToken) ? null : fcmToken;
        Touch();
    }

    /// <summary>
    /// Inscribe la cuenta en el piloto clínico activo. Está pensado para uso administrativo del equipo
    /// clínico Kaelín al reclutar pacientes; no existe un flujo de aplicación automático (acta A16).
    /// </summary>
    public void EnrollInActivePilot()
    {
        IsInActivePilot = true;
        Touch();
    }

    /// <summary>
    /// Anonimiza la cuenta del paciente en ejercicio del derecho al olvido (US26, Ley N° 29733). No es
    /// un borrado físico: reemplaza los datos personales por marcadores no reversibles, desvincula el
    /// token de dispositivo y desactiva la cuenta, preservando la clave primaria para mantener la
    /// trazabilidad de auditoría y la integridad referencial de las filas clínicas.
    /// </summary>
    /// <param name="patientRoleId">Identificador del rol paciente en el catálogo, para validar que la
    /// operación solo se aplique a pacientes.</param>
    /// <exception cref="OnlyPatientsCanBeAnonymizedException">Si la cuenta no es de un paciente.</exception>
    public void Anonymize(int patientRoleId)
    {
        if (RoleId != patientRoleId)
        {
            throw new OnlyPatientsCanBeAnonymizedException(Id);
        }

        Email = $"deleted-{Id}@anonymized.local";
        FullName = "Usuario anonimizado";
        FcmToken = null;
        Status = UserStatus.Inactive;
        Touch();
    }

    /// <summary>
    /// Indica si la cuenta está bloqueada en el momento especificado.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC de referencia.</param>
    /// <returns><see langword="true"/> si la cuenta está bloqueada.</returns>
    public bool IsLocked(DateTime utcNow)
    {
        return LockedUntil is not null && LockedUntil > utcNow;
    }

    /// <summary>
    /// Indica si la cuenta tiene el rol especificado, según el catálogo de roles
    /// provisto.
    /// </summary>
    /// <param name="roleName">Nombre del rol a verificar.</param>
    /// <param name="rolesById">Diccionario de roles por identificador.</param>
    /// <returns><see langword="true"/> si la cuenta tiene el rol indicado.</returns>
    public bool HasRole(string roleName, IReadOnlyDictionary<int, string> rolesById)
    {
        return rolesById.TryGetValue(RoleId, out var name)
            && string.Equals(name, roleName, StringComparison.Ordinal);
    }

    private void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}
