using System.ComponentModel.DataAnnotations;

namespace Cauce.Application.Recommendations.Configuration;

/// <summary>
/// Opciones de configuración del módulo de recomendaciones, ligadas a la sección
/// <c>Recommendations</c> de la configuración.
/// </summary>
public sealed class RecommendationsOptions
{
    /// <summary>
    /// Nombre de la sección de configuración.
    /// </summary>
    public const string SectionName = "Recommendations";

    /// <summary>
    /// Tipo de motor a usar ("Rule" o "Onnx").
    /// </summary>
    public string EngineKind { get; init; } = "Rule";

    /// <summary>
    /// Ruta al archivo del modelo ONNX. Si es relativa, se resuelve buscando hacia arriba desde el
    /// directorio de ejecución. Si el archivo no existe, el motor ONNX cae al motor de regla (TS07).
    /// </summary>
    public string OnnxModelPath { get; init; } = "infrastructure/models/dummy_v0.0.1.onnx";

    /// <summary>
    /// Tamaño en días de la ventana inicial de consumo para los candidatos.
    /// </summary>
    [Range(1, 60)]
    public int CandidateFoodsWindowDays { get; init; } = 14;

    /// <summary>
    /// Cantidad mínima de alimentos distintos requerida para generar una recomendación.
    /// </summary>
    [Range(1, 20)]
    public int MinCandidateFoodsCount { get; init; } = 5;

    /// <summary>
    /// Indica si la auto-aprobación está habilitada. Deshabilitada durante el piloto (DEC-B4-04).
    /// </summary>
    public bool AutoApprovalEnabled { get; init; }

    /// <summary>
    /// Umbral mínimo de confianza para auto-aprobar.
    /// </summary>
    [Range(typeof(decimal), "0", "1")]
    public decimal AutoApprovalThreshold { get; init; } = 0.95m;

    /// <summary>
    /// Cantidad máxima de ítems de tipo "evitar" tolerada para auto-aprobar.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int MaxAvoidItemsForAutoApproval { get; init; } = 3;

    /// <summary>
    /// Ventana de vigencia de una recomendación, en horas.
    /// </summary>
    [Range(1, 720)]
    public int ExpirationWindowHours { get; init; } = 72;

    /// <summary>
    /// Umbral de puntaje a partir del cual un alimento se clasifica como "evitar".
    /// </summary>
    [Range(typeof(decimal), "0", "1")]
    public decimal AvoidThreshold { get; init; } = 0.70m;

    /// <summary>
    /// Umbral de puntaje a partir del cual un alimento se clasifica como "reducir".
    /// </summary>
    [Range(typeof(decimal), "0", "1")]
    public decimal ReduceThreshold { get; init; } = 0.45m;

    /// <summary>
    /// Opciones de conexión con el modelo de lenguaje local (Ollama).
    /// </summary>
    public OllamaOptions Ollama { get; init; } = new();
}
