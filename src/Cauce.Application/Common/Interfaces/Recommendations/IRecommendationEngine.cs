using Cauce.Application.Recommendations.Contracts;

namespace Cauce.Application.Common.Interfaces.Recommendations;

/// <summary>
/// Motor de recomendaciones: produce un puntaje por alimento candidato que representa la
/// probabilidad estimada de causar síntomas. La implementación por default es la regla FODMAP
/// determinística; el modelo ONNX la reemplaza sin cambiar este contrato (DEC-B4-01).
/// </summary>
public interface IRecommendationEngine
{
    /// <summary>
    /// Descriptor de la versión del motor (nombre, hash y tipo).
    /// </summary>
    ModelVersionDescriptor Descriptor { get; }

    /// <summary>
    /// Puntúa los alimentos candidatos del paciente.
    /// </summary>
    /// <param name="input">Entrada con el contexto del paciente y los candidatos.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>El resultado con los puntajes, la confianza agregada y la versión usada.</returns>
    Task<RecommendationEngineResult> ScoreFoodsAsync(
        RecommendationEngineInput input,
        CancellationToken cancellationToken);
}
