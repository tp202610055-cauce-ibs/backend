using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using Cauce.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="IbsSssAssessment"/>.
/// Los rangos de las dimensiones y del puntaje total, y la unicidad de la línea base por
/// paciente, se refuerzan a nivel de base de datos.
/// </summary>
public sealed class IbsSssAssessmentConfiguration : IEntityTypeConfiguration<IbsSssAssessment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IbsSssAssessment> builder)
    {
        builder.ToTable("ibs_sss_assessments", table =>
        {
            table.HasCheckConstraint("ck_ibs_sss_pain_severity", "pain_severity BETWEEN 0 AND 100");
            table.HasCheckConstraint("ck_ibs_sss_pain_frequency", "pain_frequency BETWEEN 0 AND 100");
            table.HasCheckConstraint("ck_ibs_sss_bloating_severity", "bloating_severity BETWEEN 0 AND 100");
            table.HasCheckConstraint("ck_ibs_sss_bowel_habits", "bowel_habits_dissatisfaction BETWEEN 0 AND 100");
            table.HasCheckConstraint("ck_ibs_sss_life_interference", "life_interference BETWEEN 0 AND 100");
            table.HasCheckConstraint("ck_ibs_sss_total_score", "total_score BETWEEN 0 AND 500");
            table.HasCheckConstraint("ck_ibs_sss_cycle_number", "cycle_number >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("assessment_id");

        builder.Property(x => x.PatientId).HasColumnName("patient_id").IsRequired();

        builder.Property(x => x.AssessmentType)
            .HasColumnName("assessment_type")
            .HasMaxLength(20)
            .HasConversion(new SnakeCaseEnumConverter<AssessmentType>())
            .IsRequired();

        builder.Property(x => x.CycleNumber).HasColumnName("cycle_number").IsRequired();
        builder.Property(x => x.PainSeverity).HasColumnName("pain_severity").IsRequired();
        builder.Property(x => x.PainFrequency).HasColumnName("pain_frequency").IsRequired();
        builder.Property(x => x.BloatingSeverity).HasColumnName("bloating_severity").IsRequired();
        builder.Property(x => x.BowelHabitsDissatisfaction).HasColumnName("bowel_habits_dissatisfaction").IsRequired();
        builder.Property(x => x.LifeInterference).HasColumnName("life_interference").IsRequired();
        builder.Property(x => x.TotalScore).HasColumnName("total_score").IsRequired();

        builder.Property(x => x.SeverityCategory)
            .HasColumnName("severity_category")
            .HasMaxLength(20)
            .HasConversion(new SnakeCaseEnumConverter<SeverityCategory>())
            .IsRequired();

        builder.Property(x => x.CompletedAt).HasColumnName("completed_at").IsRequired();
        builder.Property(x => x.NextAssessmentDate).HasColumnName("next_assessment_date").HasColumnType("date");

        builder.HasIndex(x => x.PatientId, "ix_ibs_sss_patient_id");
        builder.HasIndex(x => x.TotalScore).HasDatabaseName("ix_ibs_sss_total_score");
        builder.HasIndex(x => x.SeverityCategory).HasDatabaseName("ix_ibs_sss_severity");

        // Unicidad parcial: a lo sumo una evaluación de línea base por paciente.
        builder.HasIndex(x => x.PatientId, "uq_ibs_sss_baseline_per_patient")
            .IsUnique()
            .HasFilter("assessment_type = 'baseline'");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
