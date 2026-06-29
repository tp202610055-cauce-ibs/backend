using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="ClinicalNote"/>. La
/// regla de asociación exclusiva y el contenido no vacío se refuerzan con CHECK constraints.
/// </summary>
public sealed class ClinicalNoteConfiguration : IEntityTypeConfiguration<ClinicalNote>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ClinicalNote> builder)
    {
        builder.ToTable("clinical_notes", table =>
        {
            table.HasCheckConstraint("ck_clinical_notes_xor", "(meal_id IS NULL) <> (symptom_id IS NULL)");
            table.HasCheckConstraint("ck_clinical_notes_content", "length(content) > 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("note_id");

        builder.Property(x => x.PatientId).HasColumnName("patient_id").IsRequired();
        builder.Property(x => x.MealId).HasColumnName("meal_id");
        builder.Property(x => x.SymptomId).HasColumnName("symptom_id");

        builder.Property(x => x.Content)
            .HasColumnName("content")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.PatientId).HasDatabaseName("ix_clinical_notes_patient_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Meal>()
            .WithMany()
            .HasForeignKey(x => x.MealId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Symptom>()
            .WithMany()
            .HasForeignKey(x => x.SymptomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
