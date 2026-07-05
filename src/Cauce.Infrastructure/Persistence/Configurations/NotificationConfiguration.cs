using Cauce.Domain.Identity;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="Notification"/>. Mapea la
/// tabla <c>notifications</c>, sus columnas en snake_case, los enums a texto, el índice parcial que
/// alimenta al despachador y el índice único filtrado que respalda el efecto exactly-once (DEC-B5-05).
/// </summary>
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("notification_id");

        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasMaxLength(20)
            .HasConversion(new SnakeCaseEnumConverter<NotificationType>())
            .IsRequired();

        builder.Property(x => x.Channel)
            .HasColumnName("channel")
            .HasMaxLength(10)
            .HasConversion(new SnakeCaseEnumConverter<NotificationChannel>())
            .IsRequired();

        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Body).HasColumnName("body").HasColumnType("text").IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion(new SnakeCaseEnumConverter<NotificationStatus>())
            .IsRequired();

        builder.Property(x => x.RetryCount).HasColumnName("retry_count").IsRequired();
        builder.Property(x => x.RelatedEntityType).HasColumnName("related_entity_type").HasMaxLength(50);
        builder.Property(x => x.RelatedEntityId).HasColumnName("related_entity_id");
        builder.Property(x => x.ScheduledFor).HasColumnName("scheduled_for").IsRequired();
        builder.Property(x => x.SentAt).HasColumnName("sent_at");
        builder.Property(x => x.DeliveredAt).HasColumnName("delivered_at");
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Índice del despachador: solo las pendientes, ordenadas por su momento de envío.
        builder.HasIndex(x => new { x.Status, x.ScheduledFor })
            .HasDatabaseName("ix_notifications_dispatch")
            .HasFilter("status = 'pending'");

        // Respaldo del efecto exactly-once: evita duplicar una notificación por (usuario, tipo,
        // entidad relacionada). Las notificaciones sin entidad relacionada (recordatorios) se excluyen.
        builder.HasIndex(x => new { x.UserId, x.Type, x.RelatedEntityType, x.RelatedEntityId })
            .HasDatabaseName("ux_notifications_dedup")
            .IsUnique()
            .HasFilter("related_entity_id IS NOT NULL");
    }
}
