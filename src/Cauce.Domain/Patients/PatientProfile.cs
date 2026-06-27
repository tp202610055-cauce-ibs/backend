using Cauce.Domain.Common;
using Cauce.Domain.Patients.Enums;
using Cauce.Domain.Patients.Exceptions;

namespace Cauce.Domain.Patients;

/// <summary>
/// Perfil clínico de un paciente con SII. Es raíz de agregado y mantiene una
/// relación 1:1 con la cuenta de usuario. Encapsula las invariantes biométricas y
/// el cálculo del índice de masa corporal (IMC).
/// </summary>
public sealed class PatientProfile : Entity, IAggregateRoot
{
    private const int MinAge = 18;
    private const int MaxAge = 120;
    private const decimal MinWeightKg = 0m;
    private const decimal MaxWeightKg = 500m;
    private const decimal MinHeightCm = 0m;
    private const decimal MaxHeightCm = 250m;
    private const int MaxMedicationsLength = 1000;

    /// <summary>
    /// Identificador de la cuenta de usuario propietaria del perfil.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Fecha de nacimiento del paciente.
    /// </summary>
    public DateOnly DateOfBirth { get; private set; }

    /// <summary>
    /// Sexo biológico del paciente.
    /// </summary>
    public BiologicalSex BiologicalSex { get; private set; }

    /// <summary>
    /// Peso del paciente en kilogramos.
    /// </summary>
    public decimal WeightKg { get; private set; }

    /// <summary>
    /// Estatura del paciente en centímetros.
    /// </summary>
    public decimal HeightCm { get; private set; }

    /// <summary>
    /// Subtipo clínico de SII.
    /// </summary>
    public IbsSubtype IbsSubtype { get; private set; }

    /// <summary>
    /// Fecha de diagnóstico de SII, si se conoce.
    /// </summary>
    public DateOnly? DiagnosisDate { get; private set; }

    /// <summary>
    /// Medicación actual del paciente, si la declara.
    /// </summary>
    public string? Medications { get; private set; }

    /// <summary>
    /// Indica si el paciente completó el proceso de onboarding clínico (incluye la
    /// evaluación IBS-SSS de línea base, que se completa en otro módulo).
    /// </summary>
    public bool OnboardingCompleted { get; private set; }

    /// <summary>
    /// Momento de creación del perfil, en UTC.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Momento de la última modificación, en UTC.
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    private PatientProfile()
    {
    }

    private PatientProfile(
        Guid id,
        Guid userId,
        DateOnly dateOfBirth,
        BiologicalSex biologicalSex,
        decimal weightKg,
        decimal heightCm,
        IbsSubtype ibsSubtype,
        DateOnly? diagnosisDate,
        string? medications,
        DateTime utcNow)
        : base(id)
    {
        UserId = userId;
        DateOfBirth = dateOfBirth;
        BiologicalSex = biologicalSex;
        WeightKg = weightKg;
        HeightCm = heightCm;
        IbsSubtype = ibsSubtype;
        DiagnosisDate = diagnosisDate;
        Medications = medications;
        OnboardingCompleted = false;
        CreatedAt = utcNow;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Crea un nuevo perfil clínico de paciente validando las invariantes biométricas.
    /// </summary>
    /// <param name="id">Identificador del perfil.</param>
    /// <param name="userId">Identificador de la cuenta de usuario.</param>
    /// <param name="dateOfBirth">Fecha de nacimiento.</param>
    /// <param name="sex">Sexo biológico.</param>
    /// <param name="weightKg">Peso en kilogramos.</param>
    /// <param name="heightCm">Estatura en centímetros.</param>
    /// <param name="subtype">Subtipo clínico de SII.</param>
    /// <param name="diagnosisDate">Fecha de diagnóstico, opcional.</param>
    /// <param name="medications">Medicación actual, opcional.</param>
    /// <param name="utcNow">Marca de tiempo UTC de creación.</param>
    /// <returns>El nuevo perfil.</returns>
    /// <exception cref="InvalidBiometricValueException">Si algún valor biométrico es inválido.</exception>
    public static PatientProfile Create(
        Guid id,
        Guid userId,
        DateOnly dateOfBirth,
        BiologicalSex sex,
        decimal weightKg,
        decimal heightCm,
        IbsSubtype subtype,
        DateOnly? diagnosisDate,
        string? medications,
        DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidBiometricValueException("El identificador del usuario es obligatorio.");
        }

        var age = ComputeAge(dateOfBirth, DateOnly.FromDateTime(utcNow));
        if (age is < MinAge or > MaxAge)
        {
            throw new InvalidBiometricValueException($"La edad del paciente debe estar entre {MinAge} y {MaxAge} años.");
        }

        if (weightKg is <= MinWeightKg or >= MaxWeightKg)
        {
            throw new InvalidBiometricValueException("El peso debe ser mayor que 0 y menor que 500 kg.");
        }

        if (heightCm is <= MinHeightCm or >= MaxHeightCm)
        {
            throw new InvalidBiometricValueException("La estatura debe ser mayor que 0 y menor que 250 cm.");
        }

        if (diagnosisDate is not null && diagnosisDate.Value > DateOnly.FromDateTime(utcNow))
        {
            throw new InvalidBiometricValueException("La fecha de diagnóstico no puede ser futura.");
        }

        if (medications is not null && medications.Length > MaxMedicationsLength)
        {
            throw new InvalidBiometricValueException("La descripción de medicación supera la longitud permitida.");
        }

        return new PatientProfile(id, userId, dateOfBirth, sex, weightKg, heightCm, subtype, diagnosisDate, medications, utcNow);
    }

    /// <summary>
    /// Calcula el índice de masa corporal (IMC) redondeado a dos decimales.
    /// </summary>
    /// <returns>El IMC en kg/m².</returns>
    public decimal CalculateBmi()
    {
        var heightM = HeightCm / 100m;
        return Math.Round(WeightKg / (heightM * heightM), 2, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Devuelve la categoría del IMC según los umbrales de la OMS.
    /// </summary>
    /// <returns><c>"underweight"</c>, <c>"normal"</c>, <c>"overweight"</c> u <c>"obese"</c>.</returns>
    public string GetBmiCategory()
    {
        return CalculateBmi() switch
        {
            < 18.5m => "underweight",
            < 25.0m => "normal",
            < 30.0m => "overweight",
            _ => "obese"
        };
    }

    /// <summary>
    /// Calcula la edad cumplida del paciente respecto a la fecha dada.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC de referencia.</param>
    /// <returns>Años cumplidos.</returns>
    public int GetAge(DateTime utcNow)
    {
        return ComputeAge(DateOfBirth, DateOnly.FromDateTime(utcNow));
    }

    /// <summary>
    /// Actualiza peso y estatura, revalidando sus rangos.
    /// </summary>
    /// <param name="weightKg">Nuevo peso en kilogramos.</param>
    /// <param name="heightCm">Nueva estatura en centímetros.</param>
    /// <param name="utcNow">Marca de tiempo UTC de la modificación.</param>
    /// <exception cref="InvalidBiometricValueException">Si algún valor es inválido.</exception>
    public void UpdateBiometrics(decimal weightKg, decimal heightCm, DateTime utcNow)
    {
        if (weightKg is <= MinWeightKg or >= MaxWeightKg)
        {
            throw new InvalidBiometricValueException("El peso debe ser mayor que 0 y menor que 500 kg.");
        }

        if (heightCm is <= MinHeightCm or >= MaxHeightCm)
        {
            throw new InvalidBiometricValueException("La estatura debe ser mayor que 0 y menor que 250 cm.");
        }

        WeightKg = weightKg;
        HeightCm = heightCm;
        Touch(utcNow);
    }

    /// <summary>
    /// Actualiza el subtipo clínico de SII.
    /// </summary>
    /// <param name="newSubtype">Nuevo subtipo.</param>
    /// <param name="utcNow">Marca de tiempo UTC de la modificación.</param>
    public void UpdateIbsSubtype(IbsSubtype newSubtype, DateTime utcNow)
    {
        IbsSubtype = newSubtype;
        Touch(utcNow);
    }

    /// <summary>
    /// Actualiza la medicación declarada.
    /// </summary>
    /// <param name="newMedications">Nueva medicación, o <see langword="null"/>.</param>
    /// <param name="utcNow">Marca de tiempo UTC de la modificación.</param>
    /// <exception cref="InvalidBiometricValueException">Si supera la longitud permitida.</exception>
    public void UpdateMedications(string? newMedications, DateTime utcNow)
    {
        if (newMedications is not null && newMedications.Length > MaxMedicationsLength)
        {
            throw new InvalidBiometricValueException("La descripción de medicación supera la longitud permitida.");
        }

        Medications = newMedications;
        Touch(utcNow);
    }

    /// <summary>
    /// Actualiza la fecha de diagnóstico.
    /// </summary>
    /// <param name="diagnosisDate">Nueva fecha de diagnóstico, o <see langword="null"/>.</param>
    /// <param name="utcNow">Marca de tiempo UTC de la modificación.</param>
    /// <exception cref="InvalidBiometricValueException">Si la fecha es futura.</exception>
    public void UpdateDiagnosisDate(DateOnly? diagnosisDate, DateTime utcNow)
    {
        if (diagnosisDate is not null && diagnosisDate.Value > DateOnly.FromDateTime(utcNow))
        {
            throw new InvalidBiometricValueException("La fecha de diagnóstico no puede ser futura.");
        }

        DiagnosisDate = diagnosisDate;
        Touch(utcNow);
    }

    /// <summary>
    /// Marca el onboarding como completado. Es idempotente: si ya estaba completado,
    /// no realiza cambios ni actualiza <see cref="UpdatedAt"/>.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC de la operación.</param>
    public void CompleteOnboarding(DateTime utcNow)
    {
        if (OnboardingCompleted)
        {
            return;
        }

        OnboardingCompleted = true;
        Touch(utcNow);
    }

    private void Touch(DateTime utcNow)
    {
        UpdatedAt = utcNow;
    }

    private static int ComputeAge(DateOnly dateOfBirth, DateOnly today)
    {
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}
