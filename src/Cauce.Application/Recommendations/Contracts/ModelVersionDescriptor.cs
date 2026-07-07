namespace Cauce.Application.Recommendations.Contracts;

/// <summary>
/// Descriptor de la versión del motor que produjo un resultado: su nombre, hash, tipo y si es un
/// modelo dummy (placeholder).
/// </summary>
/// <param name="VersionName">Nombre de la versión (por ejemplo, <c>rule-v1.0.0</c>).</param>
/// <param name="ModelHash">Hash SHA-256 del motor o artefacto.</param>
/// <param name="EngineKind">Tipo de motor ("Rule" | "Onnx").</param>
/// <param name="IsDummy">Indica si el modelo es un placeholder sin validez clínica (TS07, acta A23).</param>
public sealed record ModelVersionDescriptor(
    string VersionName,
    string ModelHash,
    string EngineKind,
    bool IsDummy = false);
