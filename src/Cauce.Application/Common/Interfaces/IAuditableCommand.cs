using Cauce.Domain.Auditing.Enums;

namespace Cauce.Application.Common.Interfaces;

/// <summary>
/// Marca un comando para auditoría automática por el <c>AuditingBehavior</c>. Solo se aplica a
/// comandos cuya entidad principal vive en una tabla SIN trigger de auditoría (DEC-B5-01, acta A8),
/// para evitar duplicación con la capa 4.
/// </summary>
public interface IAuditableCommand
{
    /// <summary>
    /// Nombre del tipo de entidad afectada, para la columna <c>entity_type</c>.
    /// </summary>
    string AuditEntityType { get; }

    /// <summary>
    /// Acción a registrar.
    /// </summary>
    AuditActionType AuditActionType { get; }

    /// <summary>
    /// Contexto adicional en JSON con identificadores del payload <b>ofuscados</b> (nunca PII cruda),
    /// o <see langword="null"/>. Como el behavior corre antes de ejecutar el handler, no hay
    /// <c>entity_id</c>; este contexto aporta la trazabilidad (por ejemplo, correo enmascarado).
    /// </summary>
    string? AuditAdditionalContext { get; }

    /// <summary>
    /// Objeto que el behavior serializa y hashea para <c>new_values_hash</c> (huella de la intención).
    /// Por defecto es el propio comando. Los comandos que transportan credenciales (contraseñas,
    /// tokens) redefinen esta propiedad devolviendo una proyección sin secretos, para no incluir
    /// credenciales en el hash de auditoría (compromiso de no almacenar hashes de contraseñas).
    /// </summary>
    object AuditPayload => this;
}
