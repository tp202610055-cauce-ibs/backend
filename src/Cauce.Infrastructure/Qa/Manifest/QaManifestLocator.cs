namespace Cauce.Infrastructure.Qa.Manifest;

/// <summary>
/// Decide qué archivo de manifiesto usar: la ruta explícita o el único <c>qa/qa-manifest*.json</c> publicado con la API.
/// </summary>
public static class QaManifestLocator
{
    /// <summary>
    /// Carpeta, relativa al directorio de la aplicación, donde se publica el manifiesto.
    /// </summary>
    public const string PublishedFolder = "qa";

    /// <summary>
    /// Devuelve la ruta del manifiesto.
    /// </summary>
    /// <param name="explicitPath">Ruta pasada con <c>--manifest</c>, o <see langword="null"/>.</param>
    /// <param name="baseDirectory">Directorio de la aplicación.</param>
    /// <exception cref="QaManifestException">Si no hay manifiesto publicado o hay más de uno.</exception>
    public static string Resolve(string? explicitPath, string baseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        var folder = Path.Combine(baseDirectory, PublishedFolder);
        var candidates = Directory.Exists(folder)
            ? Directory.GetFiles(folder, "qa-manifest*.json")
            : [];

        return candidates.Length switch
        {
            1 => candidates[0],
            0 => throw new QaManifestException([$"No hay manifiesto publicado en {folder}; indica uno con --manifest <ruta>."]),
            _ => throw new QaManifestException([$"Hay {candidates.Length} manifiestos en {folder}; indica uno con --manifest <ruta>."])
        };
    }
}
