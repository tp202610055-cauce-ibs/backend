using Cauce.Domain.ClinicalRegistry.Exceptions;
using Cauce.Domain.Common;

namespace Cauce.Domain.ClinicalRegistry;

/// <summary>
/// Nota clínica libre escrita por el paciente y asociada exactamente a una comida o a
/// un síntoma (nunca a ambos ni a ninguno). Es raíz de agregado.
/// </summary>
public sealed class ClinicalNote : Entity, IAggregateRoot
{
    private const int MaxContentLength = 500;

    /// <summary>
    /// Identificador generado en el dispositivo, usado como clave de idempotencia del alta. Es único
    /// por paciente y llega en el cuerpo o en el encabezado <c>Idempotency-Key</c>.
    /// </summary>
    public Guid ClientGuid { get; private set; }

    /// <summary>
    /// Identificador del paciente autor de la nota.
    /// </summary>
    public Guid PatientId { get; private set; }

    /// <summary>
    /// Identificador de la comida asociada, o <see langword="null"/> si la nota se asocia
    /// a un síntoma.
    /// </summary>
    public Guid? MealId { get; private set; }

    /// <summary>
    /// Identificador del síntoma asociado, o <see langword="null"/> si la nota se asocia
    /// a una comida.
    /// </summary>
    public Guid? SymptomId { get; private set; }

    /// <summary>
    /// Contenido de la nota (1–500 caracteres).
    /// </summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// Momento de creación, en UTC.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    private ClinicalNote()
    {
    }

    private ClinicalNote(Guid id, Guid clientGuid, Guid patientId, Guid? mealId, Guid? symptomId, string content, DateTime utcNow)
        : base(id)
    {
        ClientGuid = clientGuid;
        PatientId = patientId;
        MealId = mealId;
        SymptomId = symptomId;
        Content = content;
        CreatedAt = utcNow;
    }

    /// <summary>
    /// Crea una nota clínica asociada a una comida o a un síntoma.
    /// </summary>
    /// <param name="id">Identificador de la nota.</param>
    /// <param name="clientGuid">Identificador generado en el dispositivo (clave de idempotencia).</param>
    /// <param name="patientId">Identificador del paciente autor.</param>
    /// <param name="mealId">Identificador de la comida, o <see langword="null"/>.</param>
    /// <param name="symptomId">Identificador del síntoma, o <see langword="null"/>.</param>
    /// <param name="content">Contenido de la nota (1–500 caracteres).</param>
    /// <param name="utcNow">Marca de tiempo UTC de creación.</param>
    /// <returns>La nueva nota clínica.</returns>
    /// <exception cref="InvalidClinicalNoteAssociationException">Si no se asocia a exactamente una comida o un síntoma.</exception>
    /// <exception cref="ArgumentException">Si el paciente, el <c>client_guid</c> o el contenido son inválidos.</exception>
    public static ClinicalNote Attach(Guid id, Guid clientGuid, Guid patientId, Guid? mealId, Guid? symptomId, string content, DateTime utcNow)
    {
        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del paciente es obligatorio.", nameof(patientId));
        }

        if (clientGuid == Guid.Empty)
        {
            throw new ArgumentException("El client_guid de la nota es obligatorio.", nameof(clientGuid));
        }

        if (mealId.HasValue == symptomId.HasValue)
        {
            throw new InvalidClinicalNoteAssociationException();
        }

        if (string.IsNullOrWhiteSpace(content) || content.Length > MaxContentLength)
        {
            throw new ArgumentException("El contenido de la nota es obligatorio y no puede superar los 500 caracteres.", nameof(content));
        }

        return new ClinicalNote(id, clientGuid, patientId, mealId, symptomId, content, utcNow);
    }
}
