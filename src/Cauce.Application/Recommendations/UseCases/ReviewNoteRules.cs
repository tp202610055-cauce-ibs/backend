using FluentValidation;

namespace Cauce.Application.Recommendations.UseCases;

/// <summary>
/// Regla común de las notas del nutricionista al aprobar, rechazar, modificar o crear una recomendación.
/// Se mide el texto sin los espacios de los extremos, que es el que se guarda.
/// </summary>
internal static class ReviewNoteRules
{
    /// <summary>
    /// Longitud mínima de la nota, sin contar los espacios de los extremos.
    /// </summary>
    public const int MinLength = 10;

    /// <summary>
    /// Longitud máxima de la nota, sin contar los espacios de los extremos.
    /// </summary>
    public const int MaxLength = 2000;

    /// <summary>
    /// Exige una nota de 10 a 2000 caracteres una vez recortada.
    /// </summary>
    /// <param name="rule">Regla sobre la propiedad de la nota.</param>
    /// <param name="subject">Nombre de la nota en los mensajes, con su artículo; por ejemplo, «La nota clínica».</param>
    public static IRuleBuilderOptions<T, string> ReviewNote<T>(this IRuleBuilderInitial<T, string> rule, string subject)
    {
        var lowerSubject = char.ToLowerInvariant(subject[0]) + subject[1..];

        return rule
            .Cascade(CascadeMode.Stop)
            .Must(text => !string.IsNullOrWhiteSpace(text))
            .WithMessage($"Falta {lowerSubject}.")
            .Must(text => text.Trim().Length >= MinLength)
            .WithMessage($"{subject} debe tener al menos {MinLength} caracteres, sin contar los espacios de los extremos.")
            .Must(text => text.Trim().Length <= MaxLength)
            .WithMessage($"{subject} no puede superar los {MaxLength} caracteres.");
    }
}
