namespace Cauce.Domain.Recommendations.Enums;

/// <summary>
/// Origen de una recomendación. En base de datos se persiste como <c>varchar</c> en snake_case
/// (por ejemplo, <c>"engine_generated"</c>).
/// </summary>
public enum RecommendationSource
{
    /// <summary>
    /// Generada por el motor de recomendaciones (regla FODMAP u ONNX). Es el valor por defecto de
    /// todas las recomendaciones existentes tras la migración.
    /// </summary>
    EngineGenerated,

    /// <summary>
    /// Creada manualmente por un nutricionista (US29).
    /// </summary>
    Manual
}
