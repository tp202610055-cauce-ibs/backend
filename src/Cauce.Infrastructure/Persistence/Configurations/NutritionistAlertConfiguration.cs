using Cauce.Domain.Identity;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de <see cref="NutritionistAlert"/> en la tabla <c>nutritionist_alerts</c>. La clave única
/// por hecho hace idempotente la escritura desde el outbox.
/// </summary>
public sealed class NutritionistAlertConfiguration : IEntityTypeConfiguration<NutritionistAlert>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NutritionistAlert> builder)
    {
        builder.ToTable("nutritionist_alerts");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("nutritionist_alert_id");

        builder.Property(x => x.NutritionistId).HasColumnName("nutritionist_id").IsRequired();
        builder.Property(x => x.PatientId).HasColumnName("patient_id").IsRequired();
        builder.Property(x => x.Type)
            .HasColumnName("alert_type")
            .HasMaxLength(40)
            .HasConversion(new SnakeCaseEnumConverter<NutritionistAlertType>())
            .IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(x => x.ReadAt).HasColumnName("read_at");
        builder.Ignore(x => x.IsRead);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.NutritionistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.NutritionistId, x.PatientId, x.Type, x.OccurredAt })
            .IsUnique()
            .HasDatabaseName("ux_nutritionist_alerts_event");

        builder.HasIndex(x => new { x.NutritionistId, x.OccurredAt })
            .HasDatabaseName("ix_nutritionist_alerts_nutritionist_occurred");

        builder.HasIndex(x => x.PatientId)
            .HasDatabaseName("ix_nutritionist_alerts_patient");
    }
}
