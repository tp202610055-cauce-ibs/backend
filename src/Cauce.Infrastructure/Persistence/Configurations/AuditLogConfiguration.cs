using System.Text;
using Cauce.Domain.Auditing;
using Cauce.Domain.Auditing.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="AuditLog"/>.
/// Mapea la tabla <c>audit_logs</c>, sus columnas en snake_case, la conversión del
/// enum de acción a texto snake_case y los índices de consulta de la bitácora.
/// </summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    /// <summary>
    /// Convierte el enum <see cref="AuditActionType"/> a su representación en
    /// snake_case lowercase para persistirlo como <c>varchar</c>, y viceversa.
    /// </summary>
    private static readonly ValueConverter<AuditActionType, string> ActionTypeConverter =
        new(
            actionType => ToSnakeCase(actionType.ToString()),
            value => Enum.Parse<AuditActionType>(ToPascalCase(value)));

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("audit_log_id");

        builder.Property(x => x.ActorUserId)
            .HasColumnName("actor_user_id");

        builder.Property(x => x.ActionType)
            .HasColumnName("action_type")
            .HasMaxLength(50)
            .HasConversion(ActionTypeConverter)
            .IsRequired();

        builder.Property(x => x.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.EntityId)
            .HasColumnName("entity_id");

        builder.Property(x => x.OldValuesHash)
            .HasColumnName("old_values_hash")
            .HasMaxLength(64);

        builder.Property(x => x.NewValuesHash)
            .HasColumnName("new_values_hash")
            .HasMaxLength(64);

        builder.Property(x => x.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45);

        builder.Property(x => x.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500);

        builder.Property(x => x.AdditionalContext)
            .HasColumnName("additional_context")
            .HasColumnType("jsonb");

        builder.Property(x => x.OccurredAt)
            .HasColumnName("occurred_at")
            .HasDefaultValueSql("now() at time zone 'utc'")
            .IsRequired();

        builder.HasIndex(x => new { x.ActorUserId, x.OccurredAt })
            .HasDatabaseName("ix_audit_logs_actor_user_id_occurred_at")
            .IsDescending(false, true);

        builder.HasIndex(x => new { x.ActionType, x.OccurredAt })
            .HasDatabaseName("ix_audit_logs_action_type_occurred_at")
            .IsDescending(false, true);

        builder.HasIndex(x => new { x.EntityType, x.EntityId })
            .HasDatabaseName("ix_audit_logs_entity_type_entity_id")
            .HasFilter("entity_id IS NOT NULL");
    }

    /// <summary>
    /// Convierte un identificador en PascalCase a snake_case lowercase.
    /// </summary>
    /// <param name="value">Texto en PascalCase.</param>
    /// <returns>Texto en snake_case lowercase.</returns>
    private static string ToSnakeCase(string value)
    {
        var builder = new StringBuilder(value.Length + 5);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (char.IsUpper(current) && i > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Convierte un identificador en snake_case a PascalCase para resolver el
    /// valor del enum durante la lectura.
    /// </summary>
    /// <param name="value">Texto en snake_case.</param>
    /// <returns>Texto en PascalCase.</returns>
    private static string ToPascalCase(string value)
    {
        var segments = value.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var builder = new StringBuilder(value.Length);
        foreach (var segment in segments)
        {
            builder.Append(char.ToUpperInvariant(segment[0]));
            if (segment.Length > 1)
            {
                builder.Append(segment[1..]);
            }
        }

        return builder.ToString();
    }
}
