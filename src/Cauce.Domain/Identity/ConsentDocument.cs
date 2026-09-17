using System.Security.Cryptography;
using System.Text;
using Cauce.Domain.Common;

namespace Cauce.Domain.Identity;

/// <summary>
/// Texto íntegro de una versión del documento de consentimiento informado.
/// </summary>
/// <remarks>
/// <para>
/// Existe para que el comprobante en PDF pueda reproducir <b>el texto que el paciente
/// aceptó</b> y no el vigente al momento de descargarlo (HU0001 escenario 4, CP004 paso 7).
/// Antes de esta entidad, el texto vivía únicamente en <c>appsettings.Consent.Text</c> como
/// un valor actual sin historia, de modo que un cambio de redacción dejaba el PDF
/// internamente inconsistente: mostraba el hash de la versión aceptada junto al texto nuevo.
/// </para>
/// <para>
/// <b>Regla permanente.</b> Cambiar la redacción del consentimiento significa insertar una
/// fila nueva con la versión siguiente y marcarla como vigente, nunca editar el texto de una
/// versión ya existente. Sobrescribir una fila rompe la correspondencia con los
/// <c>consent_records</c> que la referencian y, con ella, la trazabilidad que exige la
/// Ley N.° 29733.
/// </para>
/// </remarks>
public sealed class ConsentDocument : Entity, IAggregateRoot
{
    /// <summary>
    /// Versión del documento. Es la clave con la que lo referencian los <c>consent_records</c>.
    /// </summary>
    public string Version { get; private set; } = string.Empty;

    /// <summary>
    /// Texto íntegro, tal cual se le presentó al paciente.
    /// </summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>
    /// Hash SHA-256 (hex, minúsculas) del texto. Se calcula al publicar la versión y permite
    /// cotejar contra el <c>consent_text_hash</c> que guardó el registro de aceptación.
    /// </summary>
    public string TextHash { get; private set; } = string.Empty;

    /// <summary>
    /// Momento de publicación de la versión, en UTC.
    /// </summary>
    public DateTime PublishedAt { get; private set; }

    /// <summary>
    /// Indica si es la versión que se le presenta hoy a quien se registra.
    /// </summary>
    public bool IsCurrent { get; private set; }

    /// <summary>
    /// Constructor sin parámetros requerido por EF Core.
    /// </summary>
    private ConsentDocument()
    {
    }

    private ConsentDocument(Guid id, string version, string text, DateTime utcNow, bool isCurrent)
        : base(id)
    {
        Version = version;
        Text = text;
        TextHash = ComputeHash(text);
        PublishedAt = utcNow;
        IsCurrent = isCurrent;
    }

    /// <summary>
    /// Publica una versión del documento de consentimiento.
    /// </summary>
    /// <param name="id">Identificador de la fila.</param>
    /// <param name="version">Versión del documento.</param>
    /// <param name="text">Texto íntegro.</param>
    /// <param name="utcNow">Marca de tiempo UTC de la publicación.</param>
    /// <param name="isCurrent">Si es la versión vigente.</param>
    /// <returns>La versión publicada.</returns>
    /// <exception cref="ArgumentException">Si la versión o el texto están vacíos.</exception>
    public static ConsentDocument Publish(
        Guid id,
        string version,
        string text,
        DateTime utcNow,
        bool isCurrent = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return new ConsentDocument(id, version, text, utcNow, isCurrent);
    }

    /// <summary>
    /// Deja de ser la versión vigente. El texto y el hash no se tocan.
    /// </summary>
    public void Supersede()
    {
        IsCurrent = false;
    }

    /// <summary>
    /// Calcula el hash SHA-256 de un texto, en hexadecimal minúsculas.
    /// </summary>
    /// <param name="text">Texto a resumir.</param>
    /// <returns>El hash en hexadecimal.</returns>
    /// <remarks>
    /// Sin normalización de ninguna clase: ni recorte, ni CRLF a LF, ni Unicode. Es el mismo
    /// cálculo que usan <see cref="ConsentRecord"/> y el servicio de consentimiento, y una
    /// diferencia de un solo byte tiene que producir un hash distinto.
    /// </remarks>
    public static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
