using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para el catálogo de alergias
/// (<see cref="Allergy"/>).
/// </summary>
public sealed class AllergyConfiguration : IEntityTypeConfiguration<Allergy>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Allergy> builder)
    {
        builder.ToTable("allergies");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("allergy_id");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.AllergyType)
            .HasColumnName("allergy_type")
            .HasMaxLength(30)
            .HasConversion(new SnakeCaseEnumConverter<AllergyType>())
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.HasIndex(x => x.Name)
            .IsUnique()
            .HasDatabaseName("ux_allergies_name");

        builder.HasIndex(x => x.IsActive)
            .HasDatabaseName("ix_allergies_is_active");
    }
}
