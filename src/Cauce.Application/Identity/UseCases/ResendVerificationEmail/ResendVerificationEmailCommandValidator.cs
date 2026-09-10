using FluentValidation;

namespace Cauce.Application.Identity.UseCases.ResendVerificationEmail;

/// <summary>
/// Validador del comando <see cref="ResendVerificationEmailCommand"/>. El endpoint es anónimo y su
/// partición de rate limit se deriva del correo, así que la entrada se valida de forma exhaustiva antes
/// de tocar cualquier lógica de negocio (regla R10 del bloque Backend-Fix-2).
/// </summary>
public sealed class ResendVerificationEmailCommandValidator : AbstractValidator<ResendVerificationEmailCommand>
{
    /// <summary>
    /// Longitud máxima de una dirección de correo según RFC 5321.
    /// </summary>
    private const int MaxEmailLength = 320;

    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public ResendVerificationEmailCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .MaximumLength(MaxEmailLength)
                .WithMessage($"El correo electrónico no puede exceder {MaxEmailLength} caracteres.")
            .EmailAddress().WithMessage("El correo electrónico no tiene un formato válido.");

        // EmailAddress de FluentValidation usa el modo compatible con ASP.NET Core, que solo comprueba
        // la posición de la arroba: acepta "con espacio@cauce.local". Aquí el correo es además la clave
        // de partición del rate limit, así que un valor con espacios ensuciaría las cubetas.
        RuleFor(x => x.Email)
            .Must(email => !email.Any(char.IsWhiteSpace))
                .WithMessage("El correo electrónico no puede contener espacios.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
