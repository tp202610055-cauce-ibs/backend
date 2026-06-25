namespace Cauce.Domain.Identity.Enums;

/// <summary>
/// Estado de un código de invitación. En base de datos se persiste como
/// <c>varchar</c> en snake_case lowercase (por ejemplo, <c>"active"</c>).
/// </summary>
public enum InvitationStatus
{
    /// <summary>
    /// Código vigente, disponible para ser consumido por un paciente.
    /// </summary>
    Active = 0,

    /// <summary>
    /// Código ya consumido por un paciente.
    /// </summary>
    Used = 1,

    /// <summary>
    /// Código vencido por superar su ventana de vigencia.
    /// </summary>
    Expired = 2
}
