using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="PatientProfile"/>.
/// Los rangos biométricos se refuerzan con CHECK constraints a nivel de base de datos.
/// </summary>
public sealed class PatientProfileConfiguration : IEntityTypeConfiguration<PatientProfile>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PatientProfile> builder)
    {
        builder.ToTable("patient_profiles", table =>
        {
            table.HasCheckConstraint("ck_patient_profiles_weight_kg", "weight_kg > 0 AND weight_kg < 500");
            table.HasCheckConstraint("ck_patient_profiles_height_cm", "height_cm > 0 AND height_cm < 250");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("profile_id");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(x => x.DateOfBirth)
            .HasColumnName("date_of_birth")
            .IsRequired();

        builder.Property(x => x.BiologicalSex)
            .HasColumnName("biological_sex")
            .HasMaxLength(10)
            .HasConversion(new SnakeCaseEnumConverter<BiologicalSex>())
            .IsRequired();

        builder.Property(x => x.WeightKg)
            .HasColumnName("weight_kg")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.HeightCm)
            .HasColumnName("height_cm")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.IbsSubtype)
            .HasColumnName("ibs_subtype")
            .HasMaxLength(10)
            .HasConversion(new SnakeCaseEnumConverter<IbsSubtype>())
            .IsRequired();

        builder.Property(x => x.DiagnosisDate)
            .HasColumnName("diagnosis_date");

        builder.Property(x => x.Medications)
            .HasColumnName("medications")
            .HasColumnType("text");

        builder.Property(x => x.OnboardingCompleted)
            .HasColumnName("onboarding_completed")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasDatabaseName("ux_patient_profiles_user_id");

        builder.HasIndex(x => x.IbsSubtype)
            .HasDatabaseName("ix_patient_profiles_ibs_subtype");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
