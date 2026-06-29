using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="CustomFood"/> y su
/// colección de ingredientes encapsulada.
/// </summary>
public sealed class CustomFoodConfiguration : IEntityTypeConfiguration<CustomFood>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CustomFood> builder)
    {
        builder.ToTable("custom_foods", table =>
        {
            table.HasCheckConstraint("ck_custom_foods_portion", "portion_size_grams > 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("custom_food_id");

        builder.Property(x => x.PatientId).HasColumnName("patient_id").IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.PortionSizeGrams)
            .HasColumnName("portion_size_grams")
            .HasPrecision(7, 2)
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => new { x.PatientId, x.Name })
            .IsUnique()
            .HasDatabaseName("uq_custom_food_per_patient");

        builder.HasIndex(x => x.PatientId).HasDatabaseName("ix_custom_foods_patient");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Ingredients)
            .WithOne()
            .HasForeignKey(x => x.CustomFoodId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Ingredients)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_ingredients");
    }
}
