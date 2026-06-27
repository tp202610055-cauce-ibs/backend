using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="NutritionistPatient"/>.
/// Incluye un índice único parcial que garantiza una sola asignación activa por par
/// nutricionista-paciente.
/// </summary>
public sealed class NutritionistPatientConfiguration : IEntityTypeConfiguration<NutritionistPatient>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NutritionistPatient> builder)
    {
        builder.ToTable("nutritionist_patient");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("assignment_id");

        builder.Property(x => x.NutritionistId)
            .HasColumnName("nutritionist_id")
            .IsRequired();

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id")
            .IsRequired();

        builder.Property(x => x.InvitationCodeId)
            .HasColumnName("invitation_code_id");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion(new SnakeCaseEnumConverter<AssignmentStatus>())
            .IsRequired();

        builder.Property(x => x.AssignedAt)
            .HasColumnName("assigned_at")
            .IsRequired();

        builder.Property(x => x.UnassignedAt)
            .HasColumnName("unassigned_at");

        builder.HasIndex(x => x.NutritionistId)
            .HasDatabaseName("ix_nutritionist_patient_nutritionist_id");

        builder.HasIndex(x => x.PatientId)
            .HasDatabaseName("ix_nutritionist_patient_patient_id");

        builder.HasIndex(x => new { x.NutritionistId, x.PatientId })
            .IsUnique()
            .HasFilter("status = 'active'")
            .HasDatabaseName("uq_nutritionist_patient_active");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.NutritionistId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<InvitationCode>()
            .WithMany()
            .HasForeignKey(x => x.InvitationCodeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
