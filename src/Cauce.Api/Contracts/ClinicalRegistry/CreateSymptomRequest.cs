using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Api.Contracts.ClinicalRegistry;

/// <summary>
/// Cuerpo de la petición de registro de un síntoma. El <see cref="ClientGuid"/> puede
/// omitirse en el cuerpo y enviarse en el encabezado <c>Idempotency-Key</c>.
/// </summary>
/// <param name="ClientGuid">Identificador del dispositivo (UUID v4), opcional si viaja en el encabezado.</param>
/// <param name="SymptomType">Tipo de síntoma.</param>
/// <param name="Intensity">Intensidad (1–100).</param>
/// <param name="OccurredAt">Momento de ocurrencia.</param>
/// <param name="ClientCreatedAt">Momento de creación en el dispositivo.</param>
public sealed record CreateSymptomRequest(
    Guid? ClientGuid,
    SymptomType SymptomType,
    int Intensity,
    DateTime OccurredAt,
    DateTime ClientCreatedAt);
