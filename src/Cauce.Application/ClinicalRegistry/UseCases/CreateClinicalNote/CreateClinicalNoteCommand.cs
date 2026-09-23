using Cauce.Application.Common.Idempotency;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.CreateClinicalNote;

/// <summary>
/// Comando para crear una nota clínica del paciente autenticado, asociada exactamente a
/// una comida o a un síntoma. Es idempotente respecto del <see cref="ClientGuid"/>, que llega en el
/// cuerpo o en el encabezado <c>Idempotency-Key</c>, igual que comidas y síntomas (DEC-B3-04).
/// </summary>
/// <param name="ClientGuid">Clave de idempotencia generada en el dispositivo (UUID v4).</param>
/// <param name="MealId">Identificador de la comida asociada, o <see langword="null"/>.</param>
/// <param name="SymptomId">Identificador del síntoma asociado, o <see langword="null"/>.</param>
/// <param name="Content">Contenido de la nota (1–500 caracteres).</param>
public sealed record CreateClinicalNoteCommand(
    Guid ClientGuid,
    Guid? MealId,
    Guid? SymptomId,
    string Content) : IRequest<CreateClinicalNoteResult>, IIdempotentCommand;
