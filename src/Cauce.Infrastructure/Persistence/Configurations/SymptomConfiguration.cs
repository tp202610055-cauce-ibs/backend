using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using Cauce.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="Symptom"/>. El rango
/// de intensidad se refuerza con un CHECK constraint.
/// </summary>
public sealed class SymptomConfiguration : IEntityTypeConfiguration<Symptom>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Symptom> builder)
    {
        builder.ToTable("symptoms", table =>
        {
            table.HasCheckConstraint("ck_symptoms_intensity", "intensity BETWEEN 1 AND 100");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("symptom_id");

        builder.Property(x => x.ClientGuid).HasColumnName("client_guid").IsRequired();
        builder.Property(x => x.PatientId).HasColumnName("patient_id").IsRequired();

        builder.Property(x => x.SymptomType)
            .HasColumnName("symptom_type")
            .HasMaxLength(50)
            .HasConversion(new SnakeCaseEnumConverter<SymptomType>())
            .IsRequired();

        builder.Property(x => x.Intensity).HasColumnName("intensity").IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(x => x.AssociatedMealId).HasColumnName("associated_meal_id");
        builder.Property(x => x.HasMealAssociation).HasColumnName("has_meal_association").IsRequired();

        builder.Property(x => x.SyncStatus)
            .HasColumnName("sync_status")
            .HasMaxLength(20)
            .HasConversion(new SnakeCaseEnumConverter<SyncStatus>())
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.ClientCreatedAt).HasColumnName("client_created_at").IsRequired();

        builder.HasIndex(x => x.ClientGuid).IsUnique().HasDatabaseName("ux_symptoms_client_guid");
        builder.HasIndex(x => x.PatientId).HasDatabaseName("ix_symptoms_patient_id");
        builder.HasIndex(x => x.ClientCreatedAt).HasDatabaseName("ix_symptoms_client_created_at");
        builder.HasIndex(x => x.HasMealAssociation).HasDatabaseName("ix_symptoms_has_meal_association");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Meal>()
            .WithMany()
            .HasForeignKey(x => x.AssociatedMealId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
