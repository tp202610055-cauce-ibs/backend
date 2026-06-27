using Cauce.Domain.Common;
using Cauce.Domain.Patients.Enums;
using Cauce.Domain.Patients.Exceptions;

namespace Cauce.Domain.Patients;

/// <summary>
/// Declaración de una alergia por parte de un paciente. Es raíz de agregado.
/// Vincula al paciente con una entrada del catálogo de alergias e indica su severidad.
/// </summary>
public sealed class PatientAllergy : Entity, IAggregateRoot
{
    private const int MaxNotesLength = 500;

    /// <summary>
    /// Identificador del paciente (cuenta de usuario) que declara la alergia.
    /// </summary>
    public Guid PatientId { get; private set; }

    /// <summary>
    /// Identificador de la entrada del catálogo de alergias.
    /// </summary>
    public Guid AllergyId { get; private set; }

    /// <summary>
    /// Severidad declarada de la alergia.
    /// </summary>
    public AllergySeverity Severity { get; private set; }

    /// <summary>
    /// Momento de la declaración, en UTC.
    /// </summary>
    public DateTime DeclaredAt { get; private set; }

    /// <summary>
    /// Notas adicionales del paciente sobre la alergia.
    /// </summary>
    public string? Notes { get; private set; }

    private PatientAllergy()
    {
    }

    private PatientAllergy(Guid id, Guid patientId, Guid allergyId, AllergySeverity severity, string? notes, DateTime utcNow)
        : base(id)
    {
        PatientId = patientId;
        AllergyId = allergyId;
        Severity = severity;
        Notes = notes;
        DeclaredAt = utcNow;
    }

    /// <summary>
    /// Registra la declaración de una alergia por un paciente.
    /// </summary>
    /// <param name="id">Identificador de la declaración.</param>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="allergyId">Identificador de la alergia del catálogo.</param>
    /// <param name="severity">Severidad declarada.</param>
    /// <param name="notes">Notas opcionales (máximo 500 caracteres).</param>
    /// <param name="utcNow">Marca de tiempo UTC de la declaración.</param>
    /// <returns>La nueva declaración.</returns>
    /// <exception cref="InvalidBiometricValueException">Si las notas superan la longitud permitida.</exception>
    public static PatientAllergy Declare(Guid id, Guid patientId, Guid allergyId, AllergySeverity severity, string? notes, DateTime utcNow)
    {
        if (patientId == Guid.Empty)
        {
            throw new InvalidBiometricValueException("El identificador del paciente es obligatorio.");
        }

        if (allergyId == Guid.Empty)
        {
            throw new InvalidBiometricValueException("El identificador de la alergia es obligatorio.");
        }

        if (notes is not null && notes.Length > MaxNotesLength)
        {
            throw new InvalidBiometricValueException("Las notas superan la longitud permitida.");
        }

        return new PatientAllergy(id, patientId, allergyId, severity, notes, utcNow);
    }

    /// <summary>
    /// Actualiza la severidad declarada.
    /// </summary>
    /// <param name="newSeverity">Nueva severidad.</param>
    /// <param name="utcNow">Marca de tiempo UTC de la modificación.</param>
    public void UpdateSeverity(AllergySeverity newSeverity, DateTime utcNow)
    {
        Severity = newSeverity;
        DeclaredAt = utcNow;
    }

    /// <summary>
    /// Actualiza las notas de la declaración.
    /// </summary>
    /// <param name="newNotes">Nuevas notas, o <see langword="null"/>.</param>
    /// <param name="utcNow">Marca de tiempo UTC de la modificación.</param>
    /// <exception cref="InvalidBiometricValueException">Si las notas superan la longitud permitida.</exception>
    public void UpdateNotes(string? newNotes, DateTime utcNow)
    {
        if (newNotes is not null && newNotes.Length > MaxNotesLength)
        {
            throw new InvalidBiometricValueException("Las notas superan la longitud permitida.");
        }

        Notes = newNotes;
        DeclaredAt = utcNow;
    }
}
