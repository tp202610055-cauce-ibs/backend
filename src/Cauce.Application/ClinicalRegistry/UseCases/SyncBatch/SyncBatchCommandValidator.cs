using FluentValidation;

namespace Cauce.Application.ClinicalRegistry.UseCases.SyncBatch;

/// <summary>
/// Validador estructural del comando <see cref="SyncBatchCommand"/>. Limita el tamaño
/// del lote a 200 elementos combinados.
/// </summary>
public sealed class SyncBatchCommandValidator : AbstractValidator<SyncBatchCommand>
{
    private const int MaxBatchSize = 200;

    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public SyncBatchCommandValidator()
    {
        RuleFor(x => x.Meals).NotNull();
        RuleFor(x => x.Symptoms).NotNull();
        RuleFor(x => x)
            .Must(command => command.Meals.Count + command.Symptoms.Count <= MaxBatchSize)
            .WithMessage($"El lote no puede superar los {MaxBatchSize} elementos combinados.");
    }
}
