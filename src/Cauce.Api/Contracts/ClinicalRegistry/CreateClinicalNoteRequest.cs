namespace Cauce.Api.Contracts.ClinicalRegistry;

/// <summary>
/// Cuerpo de la petición de creación de una nota clínica, asociada exactamente a una
/// comida o a un síntoma.
/// </summary>
/// <param name="MealId">Identificador de la comida asociada, o <see langword="null"/>.</param>
/// <param name="SymptomId">Identificador del síntoma asociado, o <see langword="null"/>.</param>
/// <param name="Content">Contenido de la nota (1–500 caracteres).</param>
/// <param name="ClientGuid">
/// Identificador generado en el dispositivo que da idempotencia al alta. Puede omitirse en el cuerpo
/// y enviarse en el encabezado <c>Idempotency-Key</c>; si viajan ambos, deben coincidir.
/// </param>
public sealed record CreateClinicalNoteRequest(
    Guid? MealId,
    Guid? SymptomId,
    string Content,
    Guid? ClientGuid = null);
