using Cauce.Domain.Common;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.Exceptions;
using Cauce.Domain.Recommendations.Services;
using Cauce.Domain.Recommendations.ValueObjects;

namespace Cauce.Domain.Recommendations;

/// <summary>
/// Recomendación dietética para un paciente con SII. Es raíz de agregado: encapsula sus
/// ítems y su retroalimentación, y gobierna las transiciones de la máquina de estados del
/// flujo Human-In-The-Loop. Solo se crea mediante la factoría <see cref="Generate"/>.
/// </summary>
public sealed class Recommendation : Entity, IAggregateRoot
{
    private readonly List<RecommendationItem> _items = new();

    /// <summary>
    /// Identificador del paciente destinatario.
    /// </summary>
    public Guid PatientId { get; private set; }

    /// <summary>
    /// Identificador de la versión de modelo que produjo la recomendación.
    /// </summary>
    public Guid ModelVersionId { get; private set; }

    /// <summary>
    /// Identificador del nutricionista que la revisó, o <see langword="null"/> si aún no fue
    /// revisada o fue auto-aprobada.
    /// </summary>
    public Guid? ReviewedByNutritionistId { get; private set; }

    /// <summary>
    /// Estado actual dentro del flujo Human-In-The-Loop.
    /// </summary>
    public RecommendationStatus Status { get; private set; }

    /// <summary>
    /// Puntaje de confianza del motor sobre la recomendación.
    /// </summary>
    public ConfidenceScore ConfidenceScore { get; private set; } = null!;

    /// <summary>
    /// Indica si la recomendación fue aprobada automáticamente, sin revisión humana.
    /// </summary>
    public bool AutoApproved { get; private set; }

    /// <summary>
    /// Nota clínica del nutricionista (de aprobación o motivo de rechazo), o <see langword="null"/>.
    /// </summary>
    public string? NutritionistNote { get; private set; }

    /// <summary>
    /// Explicación en lenguaje natural dirigida al paciente, o <see langword="null"/>.
    /// </summary>
    public string? AiExplanation { get; private set; }

    /// <summary>
    /// Origen de la explicación.
    /// </summary>
    public ExplanationSource ExplanationSource { get; private set; }

    /// <summary>
    /// Momento de generación, en UTC.
    /// </summary>
    public DateTime GeneratedAt { get; private set; }

    /// <summary>
    /// Momento de revisión (aprobación o rechazo), en UTC; <see langword="null"/> si no fue revisada.
    /// </summary>
    public DateTime? ReviewedAt { get; private set; }

    /// <summary>
    /// Momento de entrega al paciente, en UTC; <see langword="null"/> si no fue entregada.
    /// </summary>
    public DateTime? DeliveredAt { get; private set; }

    /// <summary>
    /// Momento de expiración, en UTC; <see langword="null"/> si no aplica.
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>
    /// Ítems que componen la recomendación. Siempre hay al menos uno.
    /// </summary>
    public IReadOnlyCollection<RecommendationItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Retroalimentación del paciente, o <see langword="null"/> si aún no la envió.
    /// </summary>
    public RecommendationFeedback? Feedback { get; private set; }

    private Recommendation()
    {
    }

    private Recommendation(
        Guid id,
        Guid patientId,
        Guid modelVersionId,
        ConfidenceScore confidence,
        ExplanationSource explanationSource,
        string? aiExplanation,
        DateTime now,
        TimeSpan expirationWindow)
        : base(id)
    {
        PatientId = patientId;
        ModelVersionId = modelVersionId;
        ConfidenceScore = confidence;
        ExplanationSource = explanationSource;
        AiExplanation = aiExplanation;
        Status = RecommendationStatus.Generated;
        AutoApproved = false;
        GeneratedAt = now;
        ExpiresAt = now + expirationWindow;
    }

    /// <summary>
    /// Genera una nueva recomendación en estado <see cref="RecommendationStatus.Generated"/>,
    /// incorporando sus ítems y fijando su ventana de expiración.
    /// </summary>
    /// <param name="patientId">Identificador del paciente destinatario.</param>
    /// <param name="modelVersionId">Identificador de la versión de modelo usada.</param>
    /// <param name="confidence">Puntaje de confianza del motor.</param>
    /// <param name="explanationSource">Origen de la explicación.</param>
    /// <param name="aiExplanation">Explicación en lenguaje natural, opcional.</param>
    /// <param name="items">Ítems de la recomendación (al menos uno).</param>
    /// <param name="now">Marca de tiempo UTC de generación.</param>
    /// <param name="expirationWindow">Ventana de vigencia desde la generación.</param>
    /// <returns>La nueva recomendación.</returns>
    /// <exception cref="EmptyRecommendationException">Si no se proporciona ningún ítem.</exception>
    public static Recommendation Generate(
        Guid patientId,
        Guid modelVersionId,
        ConfidenceScore confidence,
        ExplanationSource explanationSource,
        string? aiExplanation,
        IReadOnlyList<RecommendationItem> items,
        DateTime now,
        TimeSpan expirationWindow)
    {
        ArgumentNullException.ThrowIfNull(confidence);

        if (items is null || items.Count < 1)
        {
            throw new EmptyRecommendationException();
        }

        var recommendation = new Recommendation(
            Guid.NewGuid(), patientId, modelVersionId, confidence, explanationSource, aiExplanation, now, expirationWindow);

        foreach (var item in items)
        {
            item.AttachTo(recommendation.Id);
            recommendation._items.Add(item);
        }

        return recommendation;
    }

    /// <summary>
    /// Transita la recomendación a revisión pendiente.
    /// </summary>
    /// <param name="now">Marca de tiempo UTC de la operación.</param>
    /// <exception cref="InvalidRecommendationStateTransitionException">Si el estado actual no lo permite.</exception>
    public void MarkPendingReview(DateTime now)
    {
        _ = now;
        RecommendationStateMachine.EnsureTransition(Status, RecommendationStatus.PendingReview);
        Status = RecommendationStatus.PendingReview;
    }

    /// <summary>
    /// Aprueba la recomendación con la nota clínica del nutricionista.
    /// </summary>
    /// <param name="nutritionistId">Identificador del nutricionista revisor.</param>
    /// <param name="note">Nota clínica de aprobación (no vacía).</param>
    /// <param name="now">Marca de tiempo UTC de la revisión.</param>
    /// <exception cref="ArgumentException">Si la nota es vacía o solo espacios.</exception>
    /// <exception cref="InvalidRecommendationStateTransitionException">Si el estado actual no lo permite.</exception>
    public void Approve(Guid nutritionistId, string note, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("La nota clínica de aprobación es obligatoria.", nameof(note));
        }

        RecommendationStateMachine.EnsureTransition(Status, RecommendationStatus.Approved);
        Status = RecommendationStatus.Approved;
        ReviewedByNutritionistId = nutritionistId;
        NutritionistNote = note;
        ReviewedAt = now;
        AutoApproved = false;
    }

    /// <summary>
    /// Aprueba automáticamente la recomendación, sin revisión humana. Solo debe invocarse desde
    /// el caso de uso de generación cuando el guard de auto-aprobación lo permite (DEC-B4-04).
    /// </summary>
    /// <param name="now">Marca de tiempo UTC de la operación.</param>
    /// <exception cref="InvalidRecommendationStateTransitionException">Si el estado actual no lo permite.</exception>
    public void AutoApprove(DateTime now)
    {
        RecommendationStateMachine.EnsureTransition(Status, RecommendationStatus.Approved);
        Status = RecommendationStatus.Approved;
        AutoApproved = true;
        ReviewedAt = now;
    }

    /// <summary>
    /// Rechaza la recomendación con el motivo del nutricionista.
    /// </summary>
    /// <param name="nutritionistId">Identificador del nutricionista revisor.</param>
    /// <param name="reason">Motivo del rechazo (no vacío).</param>
    /// <param name="now">Marca de tiempo UTC de la revisión.</param>
    /// <exception cref="ArgumentException">Si el motivo es vacío o solo espacios.</exception>
    /// <exception cref="InvalidRecommendationStateTransitionException">Si el estado actual no lo permite.</exception>
    public void Reject(Guid nutritionistId, string reason, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("El motivo de rechazo es obligatorio.", nameof(reason));
        }

        RecommendationStateMachine.EnsureTransition(Status, RecommendationStatus.Rejected);
        Status = RecommendationStatus.Rejected;
        ReviewedByNutritionistId = nutritionistId;
        NutritionistNote = reason;
        ReviewedAt = now;
    }

    /// <summary>
    /// Marca la recomendación como entregada al paciente.
    /// </summary>
    /// <param name="now">Marca de tiempo UTC de la entrega.</param>
    /// <exception cref="InvalidRecommendationStateTransitionException">Si el estado actual no lo permite.</exception>
    public void Deliver(DateTime now)
    {
        RecommendationStateMachine.EnsureTransition(Status, RecommendationStatus.Delivered);
        Status = RecommendationStatus.Delivered;
        DeliveredAt = now;
    }

    /// <summary>
    /// Incorpora la retroalimentación del paciente y transita la recomendación al estado
    /// correspondiente.
    /// </summary>
    /// <param name="feedback">Retroalimentación a incorporar.</param>
    /// <param name="now">Marca de tiempo UTC de la operación.</param>
    /// <exception cref="InvalidRecommendationStateTransitionException">Si el estado actual no lo permite.</exception>
    public void RecordFeedback(RecommendationFeedback feedback, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        _ = now;
        RecommendationStateMachine.EnsureTransition(Status, RecommendationStatus.FeedbackReceived);
        Feedback = feedback;
        Status = RecommendationStatus.FeedbackReceived;
    }

    /// <summary>
    /// Expira la recomendación si se encuentra en un estado expirable
    /// (<see cref="RecommendationStatus.Generated"/>, <see cref="RecommendationStatus.PendingReview"/>
    /// o <see cref="RecommendationStatus.Approved"/> sin entregar). Es idempotente y no realiza
    /// cambios en estados terminales o ya entregados.
    /// </summary>
    /// <param name="now">Marca de tiempo UTC de la operación.</param>
    public void Expire(DateTime now)
    {
        _ = now;
        if (!IsExpirableState())
        {
            return;
        }

        Status = RecommendationStatus.Expired;
    }

    /// <summary>
    /// Indica si la recomendación debe considerarse expirada en una lectura: su ventana venció
    /// y aún se encuentra en un estado expirable.
    /// </summary>
    /// <param name="now">Marca de tiempo UTC de referencia.</param>
    /// <returns><see langword="true"/> si la recomendación debe transitar a expirada.</returns>
    public bool IsExpired(DateTime now)
    {
        return ExpiresAt.HasValue && ExpiresAt.Value < now && IsExpirableState();
    }

    private bool IsExpirableState()
    {
        return Status is RecommendationStatus.Generated
            or RecommendationStatus.PendingReview
            or RecommendationStatus.Approved;
    }
}
