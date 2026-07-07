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
    /// <summary>
    /// Estados en los que una recomendación es visible para el paciente (US14 CA03): los tres estados
    /// terminales aprobados más los de entrega y retroalimentación. Se excluyen los estados previos a la
    /// aprobación (<c>Generated</c>, <c>PendingReview</c>), el rechazo y la expiración. El archivado se
    /// filtra aparte por <see cref="IsActive"/>. Se incluyen <c>Delivered</c> y <c>FeedbackReceived</c>
    /// para no ocultar recomendaciones que el paciente está aplicando o ya completó (acta A24).
    /// </summary>
    public static readonly IReadOnlyList<RecommendationStatus> PatientVisibleStatuses =
    [
        RecommendationStatus.Approved,
        RecommendationStatus.ModifiedApproved,
        RecommendationStatus.ManualApproved,
        RecommendationStatus.Delivered,
        RecommendationStatus.FeedbackReceived
    ];

    private readonly List<RecommendationItem> _items = new();

    /// <summary>
    /// Identificador del paciente destinatario.
    /// </summary>
    public Guid PatientId { get; private set; }

    /// <summary>
    /// Identificador de la versión de modelo que produjo la recomendación, o <see langword="null"/>
    /// en las recomendaciones manuales (US29), que no provienen de un motor.
    /// </summary>
    public Guid? ModelVersionId { get; private set; }

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

    /// <summary>
    /// Origen de la recomendación (motor o creación manual del nutricionista).
    /// </summary>
    public RecommendationSource Source { get; private set; }

    /// <summary>
    /// Título de la recomendación; solo se completa en las recomendaciones manuales (US29).
    /// </summary>
    public string? Title { get; private set; }

    /// <summary>
    /// Descripción de la recomendación; solo se completa en las recomendaciones manuales.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Pasos accionables de la recomendación, o <see langword="null"/>. Se persiste como JSONB.
    /// </summary>
    public IReadOnlyList<string>? Steps { get; private set; }

    /// <summary>
    /// Indica si la recomendación está activa (visible para el paciente). El archivado se modela como
    /// <c>IsActive = false</c> en lugar de un estado adicional (acta A22).
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Momento de archivado, en UTC; <see langword="null"/> si está activa.
    /// </summary>
    public DateTime? ArchivedAt { get; private set; }

    /// <summary>
    /// Motivo del archivado, o <see langword="null"/> si está activa.
    /// </summary>
    public ArchiveReason? ArchiveReason { get; private set; }

    /// <summary>
    /// Fecha de vigencia de la recomendación, en UTC; <see langword="null"/> si no caduca. Al vencer,
    /// el worker de archivado la marca inactiva (US30 CA02).
    /// </summary>
    public DateTime? ValidUntil { get; private set; }

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
        Source = RecommendationSource.EngineGenerated;
        AutoApproved = false;
        IsActive = true;
        GeneratedAt = now;
        ExpiresAt = now + expirationWindow;
    }

    private Recommendation(
        Guid id,
        Guid patientId,
        Guid nutritionistId,
        string title,
        string description,
        IReadOnlyList<string>? steps,
        string clinicalNote,
        DateTime? validUntil,
        DateTime now)
        : base(id)
    {
        PatientId = patientId;
        ModelVersionId = null;
        ConfidenceScore = ConfidenceScore.Create(1.0m);
        ExplanationSource = ExplanationSource.Manual;
        AiExplanation = null;
        Status = RecommendationStatus.ManualApproved;
        Source = RecommendationSource.Manual;
        AutoApproved = false;
        IsActive = true;
        Title = title;
        Description = description;
        Steps = steps;
        NutritionistNote = clinicalNote;
        ReviewedByNutritionistId = nutritionistId;
        ReviewedAt = now;
        GeneratedAt = now;
        ValidUntil = validUntil;
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
    /// Crea una recomendación manual del nutricionista, aprobada de inmediato (US29). No proviene de un
    /// motor: <see cref="ModelVersionId"/> es <see langword="null"/>, la confianza es 1.0 y el origen de
    /// la explicación es <see cref="ExplanationSource.Manual"/>. Puede no tener ítems de catálogo.
    /// </summary>
    /// <param name="patientId">Identificador del paciente destinatario.</param>
    /// <param name="nutritionistId">Identificador del nutricionista autor.</param>
    /// <param name="title">Título de la recomendación (obligatorio).</param>
    /// <param name="description">Descripción de la recomendación (obligatoria).</param>
    /// <param name="steps">Pasos accionables, opcional.</param>
    /// <param name="clinicalNote">Nota clínica del nutricionista (obligatoria).</param>
    /// <param name="validUntil">Fecha de vigencia, o <see langword="null"/> si no caduca.</param>
    /// <param name="now">Marca de tiempo UTC de creación.</param>
    /// <returns>La nueva recomendación manual.</returns>
    /// <exception cref="ArgumentException">Si el título, la descripción o la nota clínica son vacíos.</exception>
    public static Recommendation CreateManual(
        Guid patientId,
        Guid nutritionistId,
        string title,
        string description,
        IReadOnlyList<string>? steps,
        string clinicalNote,
        DateTime? validUntil,
        DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(clinicalNote);

        var normalizedSteps = steps is { Count: > 0 } ? steps : null;

        return new Recommendation(
            Guid.NewGuid(), patientId, nutritionistId, title, description, normalizedSteps, clinicalNote, validUntil, now);
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
    /// Aprueba la recomendación tras modificarla: reemplaza sus ítems y/o su contenido y transita a
    /// <see cref="RecommendationStatus.ModifiedApproved"/> (US17 CA03). Solo válido desde
    /// <see cref="RecommendationStatus.PendingReview"/>.
    /// </summary>
    /// <param name="nutritionistId">Identificador del nutricionista revisor.</param>
    /// <param name="note">Nota clínica de la modificación (no vacía).</param>
    /// <param name="now">Marca de tiempo UTC de la revisión.</param>
    /// <param name="items">Nuevos ítems que reemplazan a los actuales, o <see langword="null"/> para conservarlos.</param>
    /// <param name="title">Nuevo título, o <see langword="null"/> para conservarlo.</param>
    /// <param name="description">Nueva descripción, o <see langword="null"/> para conservarla.</param>
    /// <param name="steps">Nuevos pasos, o <see langword="null"/> para conservarlos.</param>
    /// <exception cref="ArgumentException">Si la nota es vacía o solo espacios.</exception>
    /// <exception cref="InvalidRecommendationStateTransitionException">Si el estado actual no lo permite.</exception>
    public void ModifyByNutritionist(
        Guid nutritionistId,
        string note,
        DateTime now,
        IReadOnlyList<RecommendationItem>? items = null,
        string? title = null,
        string? description = null,
        IReadOnlyList<string>? steps = null)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("La nota clínica de la modificación es obligatoria.", nameof(note));
        }

        RecommendationStateMachine.EnsureTransition(Status, RecommendationStatus.ModifiedApproved);

        if (items is not null)
        {
            _items.Clear();
            foreach (var item in items)
            {
                item.AttachTo(Id);
                _items.Add(item);
            }
        }

        if (title is not null)
        {
            Title = title;
        }

        if (description is not null)
        {
            Description = description;
        }

        if (steps is not null)
        {
            Steps = steps.Count > 0 ? steps : null;
        }

        Status = RecommendationStatus.ModifiedApproved;
        ReviewedByNutritionistId = nutritionistId;
        NutritionistNote = note;
        ReviewedAt = now;
    }

    /// <summary>
    /// Archiva la recomendación (la marca inactiva) con el motivo indicado (US30). Solo válido sobre
    /// recomendaciones activas en un estado terminal aprobado (<see cref="RecommendationStatus.Approved"/>,
    /// <see cref="RecommendationStatus.ModifiedApproved"/> o <see cref="RecommendationStatus.ManualApproved"/>).
    /// No cambia el estado del flujo HITL: el archivado es un flag (acta A22).
    /// </summary>
    /// <param name="reason">Motivo del archivado.</param>
    /// <param name="now">Marca de tiempo UTC de la operación.</param>
    /// <exception cref="RecommendationNotArchivableException">Si no está activa o no está en un estado terminal aprobado.</exception>
    public void Archive(ArchiveReason reason, DateTime now)
    {
        if (!IsActive || !IsApprovedTerminalState())
        {
            throw new RecommendationNotArchivableException(Id);
        }

        IsActive = false;
        ArchivedAt = now;
        ArchiveReason = reason;
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

    private bool IsApprovedTerminalState()
    {
        return Status is RecommendationStatus.Approved
            or RecommendationStatus.ModifiedApproved
            or RecommendationStatus.ManualApproved;
    }

    /// <summary>
    /// Indica si la recomendación es visible para el paciente: está activa y en un estado visible
    /// (US14 CA03). El nutricionista, en cambio, ve todas las recomendaciones de sus pacientes.
    /// </summary>
    /// <returns><see langword="true"/> si el paciente puede verla.</returns>
    public bool IsVisibleToPatient()
    {
        return IsActive && PatientVisibleStatuses.Contains(Status);
    }
}
