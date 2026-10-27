using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Common;

namespace Cauce.Domain.ClinicalRegistry;

/// <summary>
/// Solicitud de un nutricionista para agregar un término al glosario.
/// </summary>
public sealed class GlossaryTermRequest : Entity
{
    /// <summary>
    /// Largo máximo del término, el mismo que el de los términos del glosario.
    /// </summary>
    public const int MaxTermLength = 100;

    /// <summary>
    /// Nutricionista que hizo la solicitud.
    /// </summary>
    public Guid NutritionistId { get; private set; }

    /// <summary>
    /// Término pedido, sin espacios en los extremos.
    /// </summary>
    public string Term { get; private set; } = string.Empty;

    /// <summary>
    /// Estado de la solicitud.
    /// </summary>
    public GlossaryTermRequestStatus Status { get; private set; }

    /// <summary>
    /// Momento de la solicitud, en UTC.
    /// </summary>
    public DateTime RequestedAt { get; private set; }

    private GlossaryTermRequest()
    {
    }

    private GlossaryTermRequest(Guid id, Guid nutritionistId, string term, DateTime requestedAt)
        : base(id)
    {
        NutritionistId = nutritionistId;
        Term = term;
        Status = GlossaryTermRequestStatus.Pending;
        RequestedAt = requestedAt;
    }

    /// <summary>
    /// Registra una solicitud pendiente con el término recortado.
    /// </summary>
    public static GlossaryTermRequest Submit(Guid nutritionistId, string term, DateTime utcNow)
    {
        if (nutritionistId == Guid.Empty)
        {
            throw new ArgumentException("El nutricionista de la solicitud es obligatorio.", nameof(nutritionistId));
        }

        var trimmed = term?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("El término es obligatorio.", nameof(term));
        }

        if (trimmed.Length > MaxTermLength)
        {
            throw new ArgumentException($"El término no puede superar los {MaxTermLength} caracteres.", nameof(term));
        }

        return new GlossaryTermRequest(Guid.NewGuid(), nutritionistId, trimmed, utcNow);
    }
}
