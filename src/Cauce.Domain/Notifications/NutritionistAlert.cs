using Cauce.Domain.Common;
using Cauce.Domain.Notifications.Enums;

namespace Cauce.Domain.Notifications;

/// <summary>
/// Aviso de la bandeja del nutricionista. Guarda solo el tipo, el paciente, el nutricionista, la fecha y
/// la lectura; el nombre del paciente se lee al listar.
/// </summary>
public sealed class NutritionistAlert : Entity
{
    /// <summary>
    /// Nutricionista que recibe el aviso.
    /// </summary>
    public Guid NutritionistId { get; private set; }

    /// <summary>
    /// Paciente al que se refiere el aviso.
    /// </summary>
    public Guid PatientId { get; private set; }

    /// <summary>
    /// Tipo de aviso.
    /// </summary>
    public NutritionistAlertType Type { get; private set; }

    /// <summary>
    /// Momento del hecho que originó el aviso, en UTC y con precisión de microsegundos.
    /// </summary>
    public DateTime OccurredAt { get; private set; }

    /// <summary>
    /// Momento en que el nutricionista lo leyó, o <see langword="null"/> si sigue sin leer.
    /// </summary>
    public DateTime? ReadAt { get; private set; }

    /// <summary>
    /// Indica si el aviso ya se leyó.
    /// </summary>
    public bool IsRead => ReadAt is not null;

    private NutritionistAlert()
    {
    }

    private NutritionistAlert(Guid id, Guid nutritionistId, Guid patientId, NutritionistAlertType type, DateTime occurredAt)
        : base(id)
    {
        NutritionistId = nutritionistId;
        PatientId = patientId;
        Type = type;
        OccurredAt = occurredAt;
    }

    /// <summary>
    /// Registra un aviso sin leer. La fecha se recorta a microsegundos, la precisión de PostgreSQL, para
    /// que el mismo evento procesado dos veces produzca la misma clave.
    /// </summary>
    public static NutritionistAlert Record(Guid nutritionistId, Guid patientId, NutritionistAlertType type, DateTime occurredAt)
    {
        if (nutritionistId == Guid.Empty)
        {
            throw new ArgumentException("El nutricionista del aviso es obligatorio.", nameof(nutritionistId));
        }

        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("El paciente del aviso es obligatorio.", nameof(patientId));
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Tipo de aviso desconocido.");
        }

        return new NutritionistAlert(Guid.NewGuid(), nutritionistId, patientId, type, TruncateToMicroseconds(occurredAt));
    }

    /// <summary>
    /// Recorta un instante a microsegundos.
    /// </summary>
    public static DateTime TruncateToMicroseconds(DateTime value) =>
        new(value.Ticks - (value.Ticks % 10), value.Kind);

    /// <summary>
    /// Marca el aviso como leído. Si ya estaba leído, conserva la primera lectura.
    /// </summary>
    public void MarkAsRead(DateTime now)
    {
        ReadAt ??= now;
    }
}
