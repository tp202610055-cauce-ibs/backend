using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Identity.Support;

/// <summary>
/// Reinicia los datos transaccionales entre pruebas de integración.
///
/// <para><b>Elección de estrategia (corrección C3):</b> se usa
/// <c>DISABLE TRIGGER ALL → TRUNCATE … RESTART IDENTITY CASCADE → ENABLE TRIGGER ALL</c> en vez de
/// <c>DELETE</c> o de la librería <c>Respawn</c>. Motivo: sobre las tablas críticas hay triggers de
/// auditoría (<c>audit_trigger_fn</c>) y sobre <c>audit_logs</c> hay un trigger de inmutabilidad. Un
/// <c>DELETE</c> dispararía los triggers de auditoría (contaminando <c>audit_logs</c> del próximo
/// test) y sería rechazado sobre <c>audit_logs</c> por su inmutabilidad. Desactivar explícitamente los
/// triggers durante el truncado garantiza un reset limpio y determinista sin filtrar filas de
/// auditoría entre pruebas. Los catálogos sembrados (roles, alergias, alimentos, versiones de modelo)
/// <b>no</b> se truncan, así que sobreviven al reset y no requieren re-siembra.</para>
/// </summary>
public static class DatabaseReset
{
    private static readonly string[] TransactionalTables =
    [
        "audit_logs",
        "notifications",
        "outbox_messages",
        "clinical_reports_metadata",
        "recommendation_feedback",
        "recommendation_items",
        "recommendations",
        "meal_items",
        "meals",
        "symptoms",
        "clinical_notes",
        "ibs_sss_assessments",
        "custom_food_ingredients",
        "custom_foods",
        "patient_allergies",
        "patient_profiles",
        "nutritionist_patient",
        "consent_records",
        "password_reset_tokens",
        "invitation_codes",
        "users"
    ];

    /// <summary>
    /// Trunca las tablas transaccionales con sus triggers desactivados, preservando los catálogos.
    /// </summary>
    /// <param name="context">Contexto de base de datos.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public static async Task ResetTransactionalDataAsync(CauceDbContext context, CancellationToken ct = default)
    {
        var quoted = string.Join(", ", TransactionalTables.Select(table => $"\"{table}\""));

        foreach (var table in TransactionalTables)
        {
            await context.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{table}\" DISABLE TRIGGER ALL;", ct).ConfigureAwait(false);
        }

        await context.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {quoted} RESTART IDENTITY CASCADE;", ct).ConfigureAwait(false);

        foreach (var table in TransactionalTables)
        {
            await context.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{table}\" ENABLE TRIGGER ALL;", ct).ConfigureAwait(false);
        }
    }
}
