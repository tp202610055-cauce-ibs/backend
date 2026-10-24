namespace Cauce.Infrastructure.Qa.Manifest;

/// <summary>
/// El manifiesto de QA no se puede usar. Lleva todos los problemas encontrados, no solo el primero.
/// </summary>
public sealed class QaManifestException : Exception
{
    /// <summary>
    /// Crea la excepción con la lista de problemas.
    /// </summary>
    public QaManifestException(IReadOnlyList<string> errors)
        : base($"El manifiesto de QA tiene {errors.Count} problema(s):{Environment.NewLine}{string.Join(Environment.NewLine, errors)}")
    {
        Errors = errors;
    }

    /// <summary>
    /// Problemas encontrados, uno por línea.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }
}
