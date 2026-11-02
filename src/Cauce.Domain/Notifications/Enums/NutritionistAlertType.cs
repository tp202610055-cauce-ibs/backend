namespace Cauce.Domain.Notifications.Enums;

/// <summary>
/// Tipo de aviso de la bandeja del nutricionista.
/// </summary>
public enum NutritionistAlertType
{
    /// <summary>
    /// Un paciente quedó vinculado al nutricionista, al registrarse con su código o desde su perfil.
    /// </summary>
    PatientLinked,

    /// <summary>
    /// Un paciente vinculado cambió su subtipo de SII.
    /// </summary>
    PatientSubtypeChanged
}
