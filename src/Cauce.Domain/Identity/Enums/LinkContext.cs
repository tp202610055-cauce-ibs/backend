namespace Cauce.Domain.Identity.Enums;

/// <summary>
/// Momento del ciclo de vida de la cuenta en el que un paciente se vinculó a un nutricionista. El
/// hecho de negocio es el mismo, pero el aviso que recibe el nutricionista cambia según cómo ocurrió
/// (acta A43).
/// </summary>
public enum LinkContext
{
    /// <summary>
    /// El paciente entregó el código de invitación durante su registro inicial.
    /// </summary>
    RegistrationLink,

    /// <summary>
    /// El paciente ya tenía cuenta y canjeó el código después, mediante
    /// <c>POST /api/v1/patients/me/nutritionist-assignment</c> (US20 CA02).
    /// </summary>
    PostRegistrationLink
}
