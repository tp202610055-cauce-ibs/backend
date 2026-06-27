using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="PatientAllergy"/>.
/// </summary>
public sealed class PatientAllergyConfiguration : IEntityTypeConfiguration<PatientAllergy>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PatientAllergy> builder)
    {
        builder.ToTable("patient_allergies");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("patient_allergy_id");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id")
            .IsRequired();

        builder.Property(x => x.AllergyId)
            .HasColumnName("allergy_id")
            .IsRequired();

        builder.Property(x => x.Severity)
            .HasColumnName("severity")
            .HasMaxLength(20)
            .HasConversion(new SnakeCaseEnumConverter<AllergySeverity>())
            .IsRequired();

        builder.Property(x => x.DeclaredAt)
            .HasColumnName("declared_at")
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.PatientId, x.AllergyId })
            .IsUnique()
            .HasDatabaseName("uq_patient_allergy");

        builder.HasIndex(x => x.PatientId)
            .HasDatabaseName("ix_patient_allergies_patient_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Allergy>()
            .WithMany()
            .HasForeignKey(x => x.AllergyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
