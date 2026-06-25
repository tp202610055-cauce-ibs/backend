namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Configuración del documento de consentimiento informado vigente. Se vincula a
/// la sección <c>Consent</c> de la configuración de la aplicación.
/// </summary>
public sealed class ConsentDocumentOptions
{
    /// <summary>
    /// Nombre de la sección de configuración asociada a estas opciones.
    /// </summary>
    public const string SectionName = "Consent";

    /// <summary>
    /// Versión vigente del documento de consentimiento.
    /// </summary>
    public required string CurrentVersion { get; init; }

    /// <summary>
    /// Texto íntegro del documento de consentimiento vigente.
    /// </summary>
    public required string Text { get; init; }
}
