using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Contracts;

namespace Cauce.Infrastructure.Recommendations.Engines;

/// <summary>
/// Placeholder del motor de recomendaciones basado en el modelo ONNX de Mirian Contreras. Lanza
/// <see cref="NotImplementedException"/> al construirse, de modo que sirve como contract test del
/// registro de DI cuando se configure <c>Recommendations:EngineKind = "Onnx"</c> sin que el modelo
/// esté disponible (DEC-B4-01).
/// </summary>
public sealed class OnnxRecommendationEngine : IRecommendationEngine
{
    /// <summary>
    /// Inicializa el motor. Lanza siempre, indicando los pasos pendientes para habilitar ONNX.
    /// </summary>
    /// <exception cref="NotImplementedException">Siempre, mientras el modelo ONNX no esté disponible.</exception>
    public OnnxRecommendationEngine()
    {
        throw new NotImplementedException(
            "OnnxRecommendationEngine no está disponible. Pendiente: (1) entrega del archivo .onnx por " +
            "Mirian Contreras, (2) entrega del archivo encoders.json con los mappings LabelEncoder, " +
            "(3) ajuste de configuración Recommendations:EngineKind a 'Onnx'. Mientras tanto, usar 'Rule' " +
            "(FodmapRuleRecommendationEngine).");
    }

    /// <inheritdoc />
    public ModelVersionDescriptor Descriptor => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<RecommendationEngineResult> ScoreFoodsAsync(
        RecommendationEngineInput input,
        CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
