using Cauce.Domain.Common;
using Cauce.Domain.Identity.Exceptions;

namespace Cauce.Domain.Identity;

/// <summary>
/// Token de restablecimiento de contraseña. Es raíz de agregado. Solo se almacena
/// el hash SHA-256 del token; el valor en claro viaja por correo y nunca se persiste.
/// Tiene vigencia fija de 30 minutos y es de uso único.
/// </summary>
public sealed class PasswordResetToken : Entity, IAggregateRoot
{
    /// <summary>
    /// Vigencia canónica de un token de restablecimiento.
    /// </summary>
    public static readonly TimeSpan Validity = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Identificador del usuario al que pertenece el token.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Hash SHA-256 (hex) del token en claro.
    /// </summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>
    /// Momento de emisión, en UTC.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Momento de expiración, en UTC.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    /// Momento de consumo, en UTC; <see langword="null"/> si no se usó.
    /// </summary>
    public DateTime? UsedAt { get; private set; }

    /// <summary>
    /// Indica si el token ya fue consumido.
    /// </summary>
    public bool IsUsed { get; private set; }

    private PasswordResetToken()
    {
    }

    private PasswordResetToken(Guid id, Guid userId, string tokenHash, DateTime utcNow, TimeSpan validity)
        : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = utcNow;
        ExpiresAt = utcNow + validity;
        IsUsed = false;
    }

    /// <summary>
    /// Emite un nuevo token de restablecimiento con vigencia de 30 minutos.
    /// </summary>
    /// <param name="id">Identificador del token.</param>
    /// <param name="userId">Identificador del usuario.</param>
    /// <param name="tokenHash">Hash SHA-256 (hex) del token en claro.</param>
    /// <param name="utcNow">Marca de tiempo UTC de emisión.</param>
    /// <param name="validity">Vigencia del token; debe ser exactamente 30 minutos.</param>
    /// <returns>El nuevo token de restablecimiento.</returns>
    /// <exception cref="ArgumentException">Si los datos no cumplen las invariantes.</exception>
    public static PasswordResetToken Issue(Guid id, Guid userId, string tokenHash, DateTime utcNow, TimeSpan validity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del usuario es obligatorio.", nameof(userId));
        }

        if (validity != Validity)
        {
            throw new ArgumentException("La vigencia del token debe ser exactamente 30 minutos.", nameof(validity));
        }

        return new PasswordResetToken(id, userId, tokenHash, utcNow, validity);
    }

    /// <summary>
    /// Indica si el token está expirado en el momento dado.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC de referencia.</param>
    /// <returns><see langword="true"/> si está expirado.</returns>
    public bool IsExpired(DateTime utcNow)
    {
        return utcNow >= ExpiresAt;
    }

    /// <summary>
    /// Indica si el token puede usarse: no consumido y no expirado.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC de referencia.</param>
    /// <returns><see langword="true"/> si es utilizable.</returns>
    public bool IsUsable(DateTime utcNow)
    {
        return !IsUsed && !IsExpired(utcNow);
    }

    /// <summary>
    /// Consume el token, marcándolo como usado.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC del consumo.</param>
    /// <exception cref="InvalidPasswordResetTokenException">Si el token ya fue usado.</exception>
    /// <exception cref="ExpiredPasswordResetTokenException">Si el token ya expiró.</exception>
    public void Consume(DateTime utcNow)
    {
        if (IsUsed)
        {
            throw new InvalidPasswordResetTokenException();
        }

        if (IsExpired(utcNow))
        {
            throw new ExpiredPasswordResetTokenException();
        }

        IsUsed = true;
        UsedAt = utcNow;
    }
}
