using System.Text.Json;
using Cauce.Application.Common.Auditing;
using Cauce.Application.Common.Interfaces;
using MediatR;

namespace Cauce.Application.Common.Behaviors;

/// <summary>
/// Comportamiento del pipeline de MediatR que audita los comandos que implementan
/// <see cref="IAuditableCommand"/> (capa 2 de DEC-B5-01, acotada a tablas sin trigger). Corre
/// <b>antes</b> de ejecutar el handler: calcula el hash del payload (intención) y enrola el
/// registro de auditoría en el <c>ChangeTracker</c>; el <c>SaveChanges</c> del handler lo persiste
/// de forma atómica. Si el handler lanza, la transacción se descarta y el audit también.
/// </summary>
/// <typeparam name="TRequest">Tipo de la petición.</typeparam>
/// <typeparam name="TResponse">Tipo de la respuesta.</typeparam>
public sealed class AuditingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// Inicializa el comportamiento con el registrador de auditoría.
    /// </summary>
    /// <param name="auditLogger">Registrador de auditoría.</param>
    public AuditingBehavior(IAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IAuditableCommand auditable)
        {
            var payload = auditable.AuditPayload;
            var newValuesHash = AuditHash.Sha256Hex(JsonSerializer.Serialize(payload, payload.GetType()));
            await _auditLogger.LogAsync(
                auditable.AuditActionType,
                auditable.AuditEntityType,
                entityId: null,
                oldValuesHash: null,
                newValuesHash,
                auditable.AuditAdditionalContext,
                cancellationToken).ConfigureAwait(false);
        }

        return await next().ConfigureAwait(false);
    }
}
