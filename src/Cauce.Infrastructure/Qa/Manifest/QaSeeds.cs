namespace Cauce.Infrastructure.Qa.Manifest;

/// <summary>
/// Elemento de siembra de un caso, según su tipo <c>t</c> en el manifiesto.
/// </summary>
public abstract record QaSeed
{
    /// <summary>
    /// Correos de cuentas que el elemento referencia.
    /// </summary>
    public virtual IEnumerable<string> ReferencedEmails() => [];
}

/// <summary>
/// Texto descriptivo del caso; la siembra lo ignora.
/// </summary>
public sealed record QaNoteSeed(string Text) : QaSeed;

/// <summary>
/// Estado explícito de una cuenta dentro de un caso; debe coincidir con la lista de cuentas.
/// </summary>
public sealed record QaAccountSeed(string Email, QaRole Role, QaAccountStatus Status) : QaSeed
{
    /// <inheritdoc />
    public override IEnumerable<string> ReferencedEmails() => [Email];
}

/// <summary>
/// Perfil clínico del paciente. Sin <see cref="Complete"/>, el paciente queda sin perfil (onboarding sin empezar).
/// </summary>
public sealed record QaProfileSeed(
    string Patient,
    bool Complete,
    decimal? WeightKg,
    decimal? HeightCm,
    string? Subtype,
    IReadOnlyList<string> Allergies) : QaSeed
{
    /// <inheritdoc />
    public override IEnumerable<string> ReferencedEmails() => [Patient];
}

/// <summary>
/// Vínculo activo entre paciente y nutricionista.
/// </summary>
public sealed record QaLinkSeed(string Patient, string Nutritionist, QaOffset? Since) : QaSeed
{
    /// <inheritdoc />
    public override IEnumerable<string> ReferencedEmails() => [Patient, Nutritionist];
}

/// <summary>
/// Comida de un solo alimento del catálogo.
/// </summary>
public sealed record QaMealSeed(string Patient, string Food, QaOffset Ago, decimal? Grams, string? Slot) : QaSeed
{
    /// <inheritdoc />
    public override IEnumerable<string> ReferencedEmails() => [Patient];
}

/// <summary>
/// Plato propio del paciente con sus ingredientes del catálogo.
/// </summary>
public sealed record QaCustomFoodSeed(string Patient, string Name, IReadOnlyList<string> Ingredients) : QaSeed
{
    /// <inheritdoc />
    public override IEnumerable<string> ReferencedEmails() => [Patient];
}

/// <summary>
/// Cuestionario IBS-SSS ya respondido, con su puntaje total.
/// </summary>
public sealed record QaIbsSeed(string Patient, bool Baseline, int Score, QaOffset Ago) : QaSeed
{
    /// <inheritdoc />
    public override IEnumerable<string> ReferencedEmails() => [Patient];
}

/// <summary>
/// Cuestionario periódico que vence hoy (día de Lima del ancla).
/// </summary>
public sealed record QaIbsDueSeed(string Patient) : QaSeed
{
    /// <inheritdoc />
    public override IEnumerable<string> ReferencedEmails() => [Patient];
}

/// <summary>
/// Cuestionario periódico que venció hace un tiempo y sigue sin responder.
/// </summary>
public sealed record QaIbsInvitationSeed(string Patient, QaOffset SentAgo, bool Answered) : QaSeed
{
    /// <inheritdoc />
    public override IEnumerable<string> ReferencedEmails() => [Patient];
}

/// <summary>
/// Síntoma registrado por el paciente. El nombre es la etiqueta en español del tipo de síntoma.
/// </summary>
public sealed record QaSymptomSeed(string Patient, string Name, int? Intensity, QaOffset Ago) : QaSeed
{
    /// <inheritdoc />
    public override IEnumerable<string> ReferencedEmails() => [Patient];
}

/// <summary>
/// Estado de una recomendación sembrada, en el vocabulario del manifiesto.
/// </summary>
public enum QaRecommendationStatus
{
    PendingReview,
    ApprovedAsGenerated,
    ApprovedModified,
    CreatedByNutritionist,
    Rejected,
    Expired
}

/// <summary>
/// Recomendación sembrada. Las del motor llevan ítems del catálogo; las manuales, título propio.
/// </summary>
public sealed record QaRecommendationSeed(
    string Patient,
    string Title,
    QaRecommendationStatus Status,
    string? Explanation,
    string? ExplanationSource,
    string? Confidence,
    IReadOnlyList<string> Suggests,
    IReadOnlyList<string> Reduce,
    IReadOnlyList<string> Avoid,
    int? ExpiresInDays,
    bool PatientOpened,
    bool PatientResponded,
    string? ClinicalNote,
    string? Description,
    int? StepsCount,
    IReadOnlyList<string> Steps,
    QaOffset? ReceivedAgo,
    QaOffset? PendingSinceAgo,
    string? ApprovedInMonth,
    int? ValidDaysLeft,
    QaSupportingData? SupportingData,
    bool WithRecentHistory) : QaSeed
{
    /// <summary>
    /// Indica si la recomendación es manual (creada por el nutricionista) y no del motor.
    /// </summary>
    public bool IsManual => Status == QaRecommendationStatus.CreatedByNutritionist;

    /// <inheritdoc />
    public override IEnumerable<string> ReferencedEmails() => [Patient];
}

/// <summary>
/// Historial que respalda una recomendación: registros de dolor en una ventana de días.
/// </summary>
public sealed record QaSupportingData(int PainRecords, int WindowDays);

/// <summary>
/// Estado de un código de invitación sembrado.
/// </summary>
public enum QaInviteCodeState
{
    Valid,
    Expired,
    Used
}

/// <summary>
/// Código de invitación del nutricionista.
/// </summary>
public sealed record QaInviteCodeSeed(
    string Nutritionist,
    QaInviteCodeState State,
    int? AgeDays,
    int? ValidHoursLeft) : QaSeed
{
    /// <inheritdoc />
    public override IEnumerable<string> ReferencedEmails() => [Nutritionist];
}

/// <summary>
/// Actividad de registro: días distintos con comidas en los últimos 14 días o en un mes fijo.
/// </summary>
public sealed record QaActivitySeed(
    string Patient,
    int? DaysWithRecordsLast14,
    int? DaysWithRecordsInMonth,
    string? Month) : QaSeed
{
    /// <inheritdoc />
    public override IEnumerable<string> ReferencedEmails() => [Patient];
}
