using Cauce.Domain.Common;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Identity.Exceptions;

namespace Cauce.Domain.Identity;

/// <summary>
/// Código de invitación generado por un nutricionista para vincular a un nuevo
/// paciente. Es raíz de agregado. Tiene una vigencia fija de 72 horas y es de
/// uso único.
/// </summary>
public sealed class InvitationCode : Entity, IAggregateRoot
{
    /// <summary>
    /// Vigencia canónica de un código de invitación.
    /// </summary>
    public static readonly TimeSpan Validity = TimeSpan.FromHours(72);

    private const int MinCodeLength = 8;
    private const int MaxCodeLength = 20;

    /// <summary>
    /// Código alfanumérico en mayúsculas, único.
    /// </summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>
    /// Identificador del nutricionista que generó el código.
    /// </summary>
    public Guid NutritionistId { get; private set; }

    /// <summary>
    /// Identificador del paciente que consumió el código; <see langword="null"/>
    /// mientras no se haya usado.
    /// </summary>
    public Guid? UsedByPatientId { get; private set; }

    /// <summary>
    /// Estado del código.
    /// </summary>
    public InvitationStatus Status { get; private set; }

    /// <summary>
    /// Momento de creación, en UTC.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Momento de expiración, en UTC.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    /// Momento en que el código fue consumido, en UTC; <see langword="null"/> si
    /// no se usó.
    /// </summary>
    public DateTime? UsedAt { get; private set; }

    private InvitationCode()
    {
    }

    private InvitationCode(Guid id, string code, Guid nutritionistId, DateTime utcNow, TimeSpan validity)
        : base(id)
    {
        Code = code;
        NutritionistId = nutritionistId;
        Status = InvitationStatus.Active;
        CreatedAt = utcNow;
        ExpiresAt = utcNow + validity;
    }

    /// <summary>
    /// Genera un nuevo código de invitación con vigencia de 72 horas.
    /// </summary>
    /// <param name="id">Identificador del código.</param>
    /// <param name="code">Código alfanumérico en mayúsculas (8 a 20 caracteres).</param>
    /// <param name="nutritionistId">Identificador del nutricionista emisor.</param>
    /// <param name="utcNow">Marca de tiempo UTC de generación.</param>
    /// <param name="validity">Vigencia del código; debe ser exactamente 72 horas.</param>
    /// <returns>El nuevo código de invitación.</returns>
    /// <exception cref="ArgumentException">Si el código o la vigencia no cumplen las invariantes.</exception>
    public static InvitationCode Generate(Guid id, string code, Guid nutritionistId, DateTime utcNow, TimeSpan validity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (code.Length is < MinCodeLength or > MaxCodeLength)
        {
            throw new ArgumentException(
                $"El código debe tener entre {MinCodeLength} y {MaxCodeLength} caracteres.", nameof(code));
        }

        if (!IsAlphanumericUpper(code))
        {
            throw new ArgumentException("El código solo puede contener caracteres alfanuméricos en mayúscula.", nameof(code));
        }

        if (validity != Validity)
        {
            throw new ArgumentException("La vigencia del código debe ser exactamente 72 horas.", nameof(validity));
        }

        if (nutritionistId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del nutricionista es obligatorio.", nameof(nutritionistId));
        }

        return new InvitationCode(id, code, nutritionistId, utcNow, validity);
    }

    /// <summary>
    /// Indica si el código está vigente y disponible en el momento dado.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC de referencia.</param>
    /// <returns><see langword="true"/> si está activo y no ha expirado.</returns>
    public bool IsValid(DateTime utcNow)
    {
        return Status == InvitationStatus.Active && ExpiresAt > utcNow;
    }

    /// <summary>
    /// Marca el código como consumido por el paciente indicado.
    /// </summary>
    /// <param name="patientId">Identificador del paciente que consume el código.</param>
    /// <param name="utcNow">Marca de tiempo UTC del consumo.</param>
    /// <exception cref="InvitationCodeAlreadyUsedException">Si el código no está activo.</exception>
    /// <exception cref="ExpiredInvitationCodeException">Si el código ya expiró.</exception>
    public void MarkAsUsed(Guid patientId, DateTime utcNow)
    {
        if (Status != InvitationStatus.Active)
        {
            throw new InvitationCodeAlreadyUsedException();
        }

        if (ExpiresAt <= utcNow)
        {
            throw new ExpiredInvitationCodeException();
        }

        Status = InvitationStatus.Used;
        UsedByPatientId = patientId;
        UsedAt = utcNow;
    }

    /// <summary>
    /// Marca el código como expirado si actualmente está activo y venció.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC de referencia.</param>
    public void Expire(DateTime utcNow)
    {
        if (Status == InvitationStatus.Active && ExpiresAt <= utcNow)
        {
            Status = InvitationStatus.Expired;
        }
    }

    private static bool IsAlphanumericUpper(string value)
    {
        foreach (var character in value)
        {
            var isUpperLetter = character is >= 'A' and <= 'Z';
            var isDigit = character is >= '0' and <= '9';
            if (!isUpperLetter && !isDigit)
            {
                return false;
            }
        }

        return true;
    }
}
