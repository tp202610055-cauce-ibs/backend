using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="GlossaryTerm"/> (US27).
/// </summary>
public sealed class GlossaryTermConfiguration : IEntityTypeConfiguration<GlossaryTerm>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GlossaryTerm> builder)
    {
        builder.ToTable("glossary_terms");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("glossary_term_id");

        builder.Property(x => x.Term)
            .HasColumnName("term")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PatientDefinition)
            .HasColumnName("patient_definition")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.NutritionistDefinition)
            .HasColumnName("nutritionist_definition")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasColumnName("category")
            .HasMaxLength(20)
            .HasConversion(new SnakeCaseEnumConverter<GlossaryCategory>())
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => x.Term).IsUnique().HasDatabaseName("ux_glossary_terms_term");
        builder.HasIndex(x => x.Category).HasDatabaseName("ix_glossary_terms_category");
    }
}
