using Cauce.Application.Common.Interfaces;
using Cauce.Domain.Auditing;
using Cauce.Domain.Auditing.Enums;
using Cauce.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Auditing;

/// <summary>
/// Implementación de <see cref="IAuditLogger"/> que enrola eventos en la tabla <c>audit_logs</c>
/// (capas 1-3 de DEC-B5-01). <b>No</b> llama a <c>SaveChangesAsync</c>: solo agrega el registro al
/// <c>ChangeTracker</c>; el <c>SaveChanges</c> del flujo que lo invoca (handler o middleware) lo
/// persiste de forma atómica junto al cambio de negocio (acta A8). Resuelve el actor a su clave
/// primaria local (acta A1) y completa IP y user agent desde <see cref="ICurrentUserService"/>.
/// </summary>
public sealed class AuditLogger : IAuditLogger
{
    private readonly CauceDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly AuditActorResolver _actorResolver;
    private readonly ILogger<AuditLogger> _logger;

    /// <summary>
    /// Inicializa el registrador de auditoría con sus dependencias.
    /// </summary>
    /// <param name="context">Contexto de base de datos.</param>
    /// <param name="currentUserService">Servicio del usuario autenticado actual.</param>
    /// <param name="actorResolver">Resolver de la clave primaria local del actor.</param>
    /// <param name="logger">Logger de la categoría del registrador.</param>
    public AuditLogger(
        CauceDbContext context,
        ICurrentUserService currentUserService,
        AuditActorResolver actorResolver,
        ILogger<AuditLogger> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _actorResolver = actorResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LogAsync(
        AuditActionType actionType,
        string entityType,
        Guid? entityId,
        string? oldValuesHash,
        string? newValuesHash,
        string? additionalContext,
        CancellationToken cancellationToken = default)
    {
        var actorLocalId = await _actorResolver.ResolveLocalActorIdAsync(cancellationToken).ConfigureAwait(false);

        if (actorLocalId is null && _currentUserService.IsAuthenticated)
        {
            // El sujeto autenticado no tiene cuenta local: se registra el evento con actor nulo para
            // no perder la traza, pero se advierte porque puede indicar una inconsistencia de datos.
            _logger.LogWarning(
                "Authenticated subject without a local user account while auditing {ActionType} on {EntityType}.",
                actionType,
                entityType);
        }

        var auditLog = AuditLog.Record(
            actorLocalId,
            actionType,
            entityType,
            entityId,
            oldValuesHash,
            newValuesHash,
            _currentUserService.IpAddress,
            _currentUserService.UserAgent,
            additionalContext,
            DateTime.UtcNow);

        await _context.AuditLogs.AddAsync(auditLog, cancellationToken).ConfigureAwait(false);
    }
}
