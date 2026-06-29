using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Infrastructure.ClinicalRegistry;

/// <summary>
/// Implementación de <see cref="IIbsSssScorer"/> que delega en el helper de dominio
/// <see cref="IbsSssScoring"/> para garantizar que la capa de aplicación y la entidad
/// usen exactamente las mismas reglas de puntuación.
/// </summary>
public sealed class IbsSssScorer : IIbsSssScorer
{
    /// <inheritdoc />
    public int CalculateTotal(int painSeverity, int painFrequency, int bloatingSeverity, int bowelHabitsDissatisfaction, int lifeInterference)
    {
        return IbsSssScoring.CalculateTotal(painSeverity, painFrequency, bloatingSeverity, bowelHabitsDissatisfaction, lifeInterference);
    }

    /// <inheritdoc />
    public SeverityCategory Categorize(int totalScore)
    {
        return IbsSssScoring.Categorize(totalScore);
    }
}
