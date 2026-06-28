using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Common;

namespace Cauce.Domain.ClinicalRegistry;

/// <summary>
/// Registro de un síntoma reportado por el paciente. Es raíz de agregado e inmutable
/// tras su creación (salvo la asociación con una comida y el estado de sincronización).
/// La asociación temporal con la comida más reciente dentro de la ventana de 4 horas la
/// calcula el servidor al sincronizar.
/// </summary>
public sealed class Symptom : Entity, IAggregateRoot
{
    private const int MinIntensity = 1;
    private const int MaxIntensity = 100;
    private const int ClockSkewToleranceMinutes = 5;

    /// <summary>
    /// Identificador estable generado en el dispositivo (UUID v4). Único por síntoma.
    /// </summary>
    public Guid ClientGuid { get; private set; }

    /// <summary>
    /// Identificador del paciente que reporta el síntoma.
    /// </summary>
    public Guid PatientId { get; private set; }

    /// <summary>
    /// Tipo de síntoma.
    /// </summary>
    public SymptomType SymptomType { get; private set; }

    /// <summary>
    /// Intensidad del síntoma, en la escala 1–100.
    /// </summary>
    public int Intensity { get; private set; }

    /// <summary>
    /// Momento en que ocurrió el síntoma.
    /// </summary>
    public DateTime OccurredAt { get; private set; }

    /// <summary>
    /// Identificador de la comida asociada por correlación temporal, o
    /// <see langword="null"/> si no hay asociación.
    /// </summary>
    public Guid? AssociatedMealId { get; private set; }

    /// <summary>
    /// Indica si el síntoma está asociado a una comida.
    /// </summary>
    public bool HasMealAssociation { get; private set; }

    /// <summary>
    /// Estado de sincronización del síntoma.
    /// </summary>
    public SyncStatus SyncStatus { get; private set; }

    /// <summary>
    /// Momento de creación en el servidor, en UTC.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Momento de creación en el dispositivo, usado para la correlación temporal con comidas.
    /// </summary>
    public DateTime ClientCreatedAt { get; private set; }

    private Symptom()
    {
    }

    private Symptom(
        Guid id,
        Guid clientGuid,
        Guid patientId,
        SymptomType symptomType,
        int intensity,
        DateTime occurredAt,
        DateTime clientCreatedAt,
        DateTime serverUtcNow)
        : base(id)
    {
        ClientGuid = clientGuid;
        PatientId = patientId;
        SymptomType = symptomType;
        Intensity = intensity;
        OccurredAt = occurredAt;
        ClientCreatedAt = clientCreatedAt;
        AssociatedMealId = null;
        HasMealAssociation = false;
        SyncStatus = SyncStatus.SyncCompleted;
        CreatedAt = serverUtcNow;
    }

    /// <summary>
    /// Registra un nuevo síntoma, validando sus invariantes.
    /// </summary>
    /// <param name="id">Identificador del síntoma.</param>
    /// <param name="clientGuid">Identificador del dispositivo (UUID v4).</param>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="type">Tipo de síntoma.</param>
    /// <param name="intensity">Intensidad (1–100).</param>
    /// <param name="occurredAt">Momento de ocurrencia.</param>
    /// <param name="clientCreatedAt">Momento de creación en el dispositivo.</param>
    /// <param name="serverUtcNow">Marca de tiempo UTC del servidor.</param>
    /// <returns>El nuevo síntoma.</returns>
    /// <exception cref="ArgumentException">Si el identificador de cliente o de paciente es inválido, o la marca temporal es futura.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si la intensidad está fuera del rango 1–100.</exception>
    public static Symptom Report(
        Guid id,
        Guid clientGuid,
        Guid patientId,
        SymptomType type,
        int intensity,
        DateTime occurredAt,
        DateTime clientCreatedAt,
        DateTime serverUtcNow)
    {
        if (clientGuid == Guid.Empty)
        {
            throw new ArgumentException("El identificador de cliente (client_guid) es obligatorio.", nameof(clientGuid));
        }

        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del paciente es obligatorio.", nameof(patientId));
        }

        if (intensity is < MinIntensity or > MaxIntensity)
        {
            throw new ArgumentOutOfRangeException(nameof(intensity), "La intensidad del síntoma debe estar entre 1 y 100.");
        }

        if (occurredAt > serverUtcNow.AddMinutes(ClockSkewToleranceMinutes))
        {
            throw new ArgumentException("La fecha de ocurrencia no puede ser futura.", nameof(occurredAt));
        }

        return new Symptom(id, clientGuid, patientId, type, intensity, occurredAt, clientCreatedAt, serverUtcNow);
    }

    /// <summary>
    /// Asocia el síntoma con una comida por correlación temporal.
    /// </summary>
    /// <param name="mealId">Identificador de la comida asociada.</param>
    /// <param name="utcNow">Marca de tiempo UTC de la operación.</param>
    public void AssociateWithMeal(Guid mealId, DateTime utcNow)
    {
        _ = utcNow;
        AssociatedMealId = mealId;
        HasMealAssociation = true;
    }

    /// <summary>
    /// Limpia la asociación del síntoma con cualquier comida.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC de la operación.</param>
    public void ClearMealAssociation(DateTime utcNow)
    {
        _ = utcNow;
        AssociatedMealId = null;
        HasMealAssociation = false;
    }

    /// <summary>
    /// Marca el síntoma como sincronizado. Es idempotente.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC de la operación.</param>
    public void MarkAsSynced(DateTime utcNow)
    {
        _ = utcNow;
        SyncStatus = SyncStatus.SyncCompleted;
    }
}
