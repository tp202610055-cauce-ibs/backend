using Cauce.Domain.Identity;
using Cauce.Domain.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="ClinicalReportMetadata"/>.
/// Mapea la tabla <c>clinical_reports_metadata</c> y sus columnas en snake_case. No persiste ni la
/// contraseña ni el URL prefirmado del reporte (DEC-B5-11).
/// </summary>
public sealed class ClinicalReportMetadataConfiguration : IEntityTypeConfiguration<ClinicalReportMetadata>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ClinicalReportMetadata> builder)
    {
        builder.ToTable("clinical_reports_metadata");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("clinical_report_metadata_id");

        builder.Property(x => x.PatientId).HasColumnName("patient_id").IsRequired();
        builder.Property(x => x.GeneratedByNutritionistId).HasColumnName("generated_by_nutritionist_id").IsRequired();
        builder.Property(x => x.PeriodStart).HasColumnName("period_start").IsRequired();
        builder.Property(x => x.PeriodEnd).HasColumnName("period_end").IsRequired();
        builder.Property(x => x.ObjectStoragePath).HasColumnName("object_storage_path").HasMaxLength(500).IsRequired();
        builder.Property(x => x.FileSizeBytes).HasColumnName("file_size_bytes").IsRequired();
        builder.Property(x => x.GeneratedAt).HasColumnName("generated_at").IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.GeneratedByNutritionistId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.PatientId, x.GeneratedAt })
            .HasDatabaseName("ix_clinical_reports_metadata_patient_generated");
    }
}
