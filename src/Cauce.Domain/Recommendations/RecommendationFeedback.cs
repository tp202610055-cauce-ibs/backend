using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Common;
using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Domain.Recommendations;

/// <summary>
/// Retroalimentación clínica del paciente sobre una recomendación entregada: si la aplicó,
/// el resultado percibido y un comentario opcional. Es entidad interna del agregado
/// <see cref="Recommendation"/> con relación uno a uno.
/// </summary>
public sealed class RecommendationFeedback : Entity
{
    /// <summary>
    /// Etiqueta de entrenamiento para un feedback que indica empeoramiento.
    /// </summary>
    private const int WorseningLabel = 1;

    /// <summary>
    /// Etiqueta de entrenamiento para un feedback que no indica empeoramiento.
    /// </summary>
    private const int NonWorseningLabel = 0;

    /// <summary>
    /// Identificador de la recomendación a la que pertenece la retroalimentación.
    /// </summary>
    public Guid RecommendationId { get; private set; }

    /// <summary>
    /// Indica si el paciente aplicó la recomendación.
    /// </summary>
    public bool WasApplied { get; private set; }

    /// <summary>
    /// Resultado clínico percibido por el paciente.
    /// </summary>
    public FeedbackOutcome Outcome { get; private set; }

    /// <summary>
    /// Comentario adicional del paciente, o <see langword="null"/>.
    /// </summary>
    public string? Comment { get; private set; }

    /// <summary>
    /// Estado de sincronización de la retroalimentación.
    /// </summary>
    public SyncStatus SyncStatus { get; private set; }

    /// <summary>
    /// Momento de envío de la retroalimentación, en UTC.
    /// </summary>
    public DateTime SubmittedAt { get; private set; }

    private RecommendationFeedback()
    {
    }

    private RecommendationFeedback(
        Guid id,
        Guid recommendationId,
        bool wasApplied,
        FeedbackOutcome outcome,
        string? comment,
        SyncStatus syncStatus,
        DateTime submittedAt)
        : base(id)
    {
        RecommendationId = recommendationId;
        WasApplied = wasApplied;
        Outcome = outcome;
        Comment = comment;
        SyncStatus = syncStatus;
        SubmittedAt = submittedAt;
    }

    /// <summary>
    /// Registra la retroalimentación del paciente sobre una recomendación.
    /// </summary>
    /// <param name="recommendationId">Identificador de la recomendación.</param>
    /// <param name="wasApplied">Si el paciente aplicó la recomendación.</param>
    /// <param name="outcome">Resultado clínico percibido.</param>
    /// <param name="comment">Comentario opcional.</param>
    /// <param name="syncStatus">Estado de sincronización.</param>
    /// <param name="submittedAt">Momento de envío, en UTC.</param>
    /// <returns>La nueva retroalimentación.</returns>
    public static RecommendationFeedback Submit(
        Guid recommendationId,
        bool wasApplied,
        FeedbackOutcome outcome,
        string? comment,
        SyncStatus syncStatus,
        DateTime submittedAt)
    {
        return new RecommendationFeedback(
            Guid.NewGuid(), recommendationId, wasApplied, outcome, comment, syncStatus, submittedAt);
    }

    /// <summary>
    /// Deriva la etiqueta supervisada para reentrenamiento del modelo: 1 si la recomendación
    /// fue contraproducente (empeoramiento), 0 en caso contrario. Solo es válida cuando el
    /// paciente efectivamente aplicó la recomendación.
    /// </summary>
    /// <returns>La etiqueta de entrenamiento (0 o 1).</returns>
    /// <exception cref="InvalidOperationException">Si la recomendación no fue aplicada y, por
    /// tanto, no permite evaluar un resultado clínico real.</exception>
    public int ToTrainingLabel()
    {
        if (!WasApplied)
        {
            throw new InvalidOperationException(
                "No se puede derivar una etiqueta de entrenamiento de una recomendación que no fue aplicada.");
        }

        return Outcome == FeedbackOutcome.Worsening ? WorseningLabel : NonWorseningLabel;
    }
}
