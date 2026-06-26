using Cauce.Application.Common.Interfaces;
using Cauce.Domain.Auditing;
using Cauce.Domain.Auditing.Enums;
using Cauce.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Auditing;

/// <summary>
/// Implementación de <see cref="IAuditLogger"/> que persiste eventos en la tabla
/// <c>audit_logs</c>. Completa los datos del actor (usuario, IP, user agent) a
/// partir de <see cref="ICurrentUserService"/> y el instante del evento con la
/// hora UTC actual.
/// </summary>
public sealed class AuditLogger : IAuditLogger
{
    private readonly CauceDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AuditLogger> _logger;

    /// <summary>
    /// Inicializa el registrador de auditoría con sus dependencias.
    /// </summary>
    /// <param name="context">Contexto de base de datos.</param>
    /// <param name="currentUserService">Servicio del usuario autenticado actual.</param>
    /// <param name="logger">Logger de la categoría del registrador.</param>
    public AuditLogger(
        CauceDbContext context,
        ICurrentUserService currentUserService,
        ILogger<AuditLogger> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
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
        var auditLog = new AuditLog(
            Guid.NewGuid(),
            _currentUserService.UserId,
            actionType,
            entityType,
            entityId,
            oldValuesHash,
            newValuesHash,
            _currentUserService.IpAddress,
            _currentUserService.UserAgent,
            additionalContext,
            DateTime.UtcNow);

        try
        {
            await _context.AuditLogs.AddAsync(auditLog, cancellationToken).ConfigureAwait(false);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to persist audit log for action {ActionType} on entity {EntityType}",
                actionType,
                entityType);
            throw;
        }
    }
}
