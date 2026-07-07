using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="IbsSssAssessmentSchedule"/> (US12).
/// </summary>
public sealed class IbsSssAssessmentScheduleConfiguration : IEntityTypeConfiguration<IbsSssAssessmentSchedule>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IbsSssAssessmentSchedule> builder)
    {
        builder.ToTable("ibs_sss_schedules");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("schedule_id");

        builder.Property(x => x.PatientId).HasColumnName("patient_id").IsRequired();
        builder.Property(x => x.DueDate).HasColumnName("due_date").IsRequired();
        builder.Property(x => x.Completed).HasColumnName("completed").IsRequired();
        builder.Property(x => x.Missed).HasColumnName("missed").IsRequired();
        builder.Property(x => x.ReminderSentAt).HasColumnName("reminder_sent_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.PatientId, x.Completed, x.Missed })
            .HasDatabaseName("ix_ibs_sss_schedules_patient_open");
        builder.HasIndex(x => x.DueDate).HasDatabaseName("ix_ibs_sss_schedules_due_date");
    }
}
