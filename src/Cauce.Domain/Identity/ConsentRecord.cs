using System.Security.Cryptography;
using System.Text;
using Cauce.Domain.Common;

namespace Cauce.Domain.Identity;

/// <summary>
/// Registro de aceptación del consentimiento informado. Es raíz de agregado e
/// inmutable tras su creación, salvo el campo <see cref="IsCurrent"/>. La
/// inmutabilidad se refuerza con triggers de base de datos. Satisface el principio
/// de no repudio del consentimiento exigido por la Ley N° 29733.
/// </summary>
public sealed class ConsentRecord : Entity, IAggregateRoot
{
    /// <summary>
    /// Identificador del usuario que aceptó el consentimiento.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Versión del documento de consentimiento aceptado.
    /// </summary>
    public string DocumentVersion { get; private set; } = string.Empty;

    /// <summary>
    /// Momento de aceptación, en UTC.
    /// </summary>
    public DateTime AcceptedAt { get; private set; }

    /// <summary>
    /// Dirección IP de origen al momento de la aceptación; <see langword="null"/>
    /// si no se pudo determinar.
    /// </summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    /// Hash SHA-256 (hex) del texto íntegro del consentimiento aceptado.
    /// </summary>
    public string ConsentTextHash { get; private set; } = string.Empty;

    /// <summary>
    /// Indica si este es el consentimiento vigente del usuario. Es el único campo
    /// modificable tras la creación.
    /// </summary>
    public bool IsCurrent { get; internal set; }

    private ConsentRecord()
    {
    }

    private ConsentRecord(Guid id, Guid userId, string documentVersion, string consentTextHash, string? ipAddress, DateTime utcNow)
        : base(id)
    {
        UserId = userId;
        DocumentVersion = documentVersion;
        ConsentTextHash = consentTextHash;
        IpAddress = ipAddress;
        AcceptedAt = utcNow;
        IsCurrent = true;
    }

    /// <summary>
    /// Captura un nuevo registro de consentimiento como vigente.
    /// </summary>
    /// <param name="id">Identificador del registro.</param>
    /// <param name="userId">Identificador del usuario.</param>
    /// <param name="documentVersion">Versión del documento aceptado.</param>
    /// <param name="consentTextHash">Hash SHA-256 (hex) del texto aceptado.</param>
    /// <param name="ipAddress">Dirección IP de origen, o <see langword="null"/>.</param>
    /// <param name="utcNow">Marca de tiempo UTC de aceptación.</param>
    /// <returns>El nuevo registro de consentimiento.</returns>
    /// <exception cref="ArgumentException">Si los datos obligatorios faltan.</exception>
    public static ConsentRecord Capture(Guid id, Guid userId, string documentVersion, string consentTextHash, string? ipAddress, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(consentTextHash);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del usuario es obligatorio.", nameof(userId));
        }

        return new ConsentRecord(id, userId, documentVersion, consentTextHash, ipAddress, utcNow);
    }

    /// <summary>
    /// Verifica la integridad del consentimiento comparando el hash almacenado con
    /// el hash SHA-256 del texto proporcionado.
    /// </summary>
    /// <param name="acceptedText">Texto del consentimiento a verificar.</param>
    /// <returns><see langword="true"/> si el texto coincide con el hash almacenado.</returns>
    public bool VerifyIntegrity(string acceptedText)
    {
        var computedHash = ComputeHash(acceptedText);
        return string.Equals(computedHash, ConsentTextHash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Marca este registro como no vigente, típicamente al aceptarse una nueva
    /// versión del documento.
    /// </summary>
    public void Supersede()
    {
        IsCurrent = false;
    }

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
