using Cauce.Domain.ClinicalRegistry;
using FluentValidation;

namespace Cauce.Application.ClinicalRegistry.UseCases.RequestGlossaryTerm;

/// <summary>
/// Validador de <see cref="RequestGlossaryTermCommand"/>. El largo se mide sin los espacios de los extremos.
/// </summary>
public sealed class RequestGlossaryTermCommandValidator : AbstractValidator<RequestGlossaryTermCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public RequestGlossaryTermCommandValidator()
    {
        RuleFor(x => (x.Term ?? string.Empty).Trim())
            .NotEmpty()
            .WithName("term")
            .WithMessage("El término es obligatorio.")
            .MaximumLength(GlossaryTermRequest.MaxTermLength)
            .WithName("term")
            .WithMessage($"El término no puede superar los {GlossaryTermRequest.MaxTermLength} caracteres.");
    }
}
