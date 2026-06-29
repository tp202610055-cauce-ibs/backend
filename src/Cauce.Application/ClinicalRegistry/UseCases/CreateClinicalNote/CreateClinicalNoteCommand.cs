using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.CreateClinicalNote;

/// <summary>
/// Comando para crear una nota clínica del paciente autenticado, asociada exactamente a
/// una comida o a un síntoma.
/// </summary>
/// <param name="MealId">Identificador de la comida asociada, o <see langword="null"/>.</param>
/// <param name="SymptomId">Identificador del síntoma asociado, o <see langword="null"/>.</param>
/// <param name="Content">Contenido de la nota (1–500 caracteres).</param>
public sealed record CreateClinicalNoteCommand(Guid? MealId, Guid? SymptomId, string Content) : IRequest<CreateClinicalNoteResult>;
