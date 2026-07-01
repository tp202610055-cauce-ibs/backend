using Cauce.Domain.Common;

namespace Cauce.Domain.Recommendations;

/// <summary>
/// Versión del motor de recomendaciones (regla determinística o modelo ONNX). Es raíz de
/// agregado y la base de la trazabilidad algorítmica: cada recomendación queda vinculada a
/// la versión que la produjo. A nivel de base de datos, a lo sumo una versión está activa
/// simultáneamente (índice único filtrado).
/// </summary>
public sealed class ModelVersion : Entity, IAggregateRoot
{
    /// <summary>
    /// Nombre único y legible de la versión (por ejemplo, <c>rule-v1.0.0</c>).
    /// </summary>
    public string VersionName { get; private set; } = string.Empty;

    /// <summary>
    /// Hash SHA-256 del motor o del artefacto del modelo, para verificación de integridad.
    /// </summary>
    public string ModelHash { get; private set; } = string.Empty;

    /// <summary>
    /// Tamaño del dataset de entrenamiento, o <see langword="null"/> si no aplica.
    /// </summary>
    public int? TrainingDatasetSize { get; private set; }

    /// <summary>
    /// Métricas de desempeño serializadas como JSON.
    /// </summary>
    public string PerformanceMetricsJson { get; private set; } = string.Empty;

    /// <summary>
    /// Momento de despliegue de la versión, en UTC.
    /// </summary>
    public DateTime DeployedAt { get; private set; }

    /// <summary>
    /// Identidad responsable del despliegue (por ejemplo, <c>system-seeder</c>).
    /// </summary>
    public string DeployedBy { get; private set; } = string.Empty;

    /// <summary>
    /// Indica si la versión está activa. A lo sumo una versión lo está simultáneamente.
    /// </summary>
    public bool IsActive { get; private set; }

    private ModelVersion()
    {
    }

    private ModelVersion(
        Guid id,
        string versionName,
        string modelHash,
        int? trainingDatasetSize,
        string performanceMetricsJson,
        string deployedBy,
        DateTime deployedAt)
        : base(id)
    {
        VersionName = versionName;
        ModelHash = modelHash;
        TrainingDatasetSize = trainingDatasetSize;
        PerformanceMetricsJson = performanceMetricsJson;
        DeployedBy = deployedBy;
        DeployedAt = deployedAt;
        IsActive = false;
    }

    /// <summary>
    /// Registra una nueva versión de modelo, inicialmente inactiva.
    /// </summary>
    /// <param name="versionName">Nombre único de la versión.</param>
    /// <param name="modelHash">Hash SHA-256 del motor o artefacto.</param>
    /// <param name="trainingDatasetSize">Tamaño del dataset de entrenamiento, opcional.</param>
    /// <param name="performanceMetricsJson">Métricas de desempeño en JSON.</param>
    /// <param name="deployedBy">Identidad responsable del despliegue.</param>
    /// <param name="deployedAt">Momento de despliegue, en UTC.</param>
    /// <returns>La nueva versión de modelo.</returns>
    /// <exception cref="ArgumentException">Si el nombre, el hash o el responsable son vacíos.</exception>
    public static ModelVersion Register(
        string versionName,
        string modelHash,
        int? trainingDatasetSize,
        string performanceMetricsJson,
        string deployedBy,
        DateTime deployedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(deployedBy);

        return new ModelVersion(
            Guid.NewGuid(), versionName, modelHash, trainingDatasetSize, performanceMetricsJson, deployedBy, deployedAt);
    }

    /// <summary>
    /// Marca la versión como activa. La unicidad de la versión activa se garantiza a nivel de
    /// base de datos mediante un índice único filtrado.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Marca la versión como inactiva.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Verifica la integridad de la versión comparando su hash con el hash actual del motor o
    /// artefacto, sin distinguir mayúsculas de minúsculas.
    /// </summary>
    /// <param name="actualHash">Hash actual a comparar.</param>
    /// <returns><see langword="true"/> si los hashes coinciden.</returns>
    public bool VerifyIntegrity(string actualHash)
    {
        return string.Equals(ModelHash, actualHash, StringComparison.OrdinalIgnoreCase);
    }
}
