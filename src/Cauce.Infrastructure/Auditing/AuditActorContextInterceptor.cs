using System.Data;
using Cauce.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cauce.Infrastructure.Auditing;

/// <summary>
/// Interceptor que propaga el actor autenticado a la variable de configuración de PostgreSQL
/// <c>cauce.actor_user_id</c> antes de cada <c>SaveChanges</c>, para que los triggers de auditoría
/// (capa 4 de DEC-B5-01) registren quién originó el cambio. La resolución keycloak→local se hace en
/// el propio SQL (subconsulta), evitando consultas EF reentrantes dentro del interceptor. Se usa una
/// variable de nivel de sesión (no <c>is_local</c>) porque un <c>SaveChanges</c> de una sola sentencia
/// no abre transacción explícita; se reinicia tras cada operación para no filtrarla entre peticiones
/// que reutilicen la conexión del pool (acta A1, A6).
///
/// <para><b>Acta A15:</b> la variable se fija ejecutando <c>set_config</c> sobre la <b>misma</b>
/// conexión del contexto, abriéndola antes si estaba cerrada. Usar
/// <c>Database.ExecuteSqlRawAsync</c> abría y cerraba una conexión propia, por lo que la variable de
/// sesión no persistía hasta el <c>INSERT</c> del save y el trigger registraba <c>actor_user_id</c>
/// nulo. Al mantener abierta la conexión del contexto, el <c>SaveChanges</c> reutiliza la misma y el
/// trigger ve el actor correcto.</para>
/// </summary>
public sealed class AuditActorContextInterceptor : SaveChangesInterceptor
{
    private const string SetActorSql =
        "SELECT set_config('cauce.actor_user_id', " +
        "COALESCE((SELECT user_id::text FROM users WHERE keycloak_id = @keycloakId), ''), false)";

    private const string ResetActorSql =
        "SELECT set_config('cauce.actor_user_id', '', false)";

    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Inicializa el interceptor con el servicio del usuario autenticado actual.
    /// </summary>
    /// <param name="currentUserService">Servicio del usuario autenticado actual.</param>
    public AuditActorContextInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            var keycloakSubject = _currentUserService.UserId?.ToString() ?? string.Empty;
            await SetActorOnConnectionAsync(eventData.Context, keycloakSubject, cancellationToken).ConfigureAwait(false);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SetActorOnConnectionAsync(DbContext context, string keycloakSubject, CancellationToken ct)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            // Se abre la conexión del contexto para que el SaveChanges reutilice la misma y la variable
            // de sesión sobreviva hasta el INSERT (acta A15). No se cierra aquí: el contexto la libera
            // al disponerse (fin del scope de la petición).
            await context.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = SetActorSql;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "keycloakId";
        parameter.Value = keycloakSubject;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await ResetActorAsync(eventData.Context, cancellationToken).ConfigureAwait(false);
        return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await ResetActorAsync(eventData.Context, cancellationToken).ConfigureAwait(false);
        await base.SaveChangesFailedAsync(eventData, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ResetActorAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null)
        {
            return;
        }

        try
        {
            await context.Database.ExecuteSqlRawAsync(ResetActorSql, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // El reinicio es defensa en profundidad: cada SaveChanges reescribe la variable antes de su
            // DML, así que un fallo aquí no compromete la correcta atribución del actor.
        }
    }
}
