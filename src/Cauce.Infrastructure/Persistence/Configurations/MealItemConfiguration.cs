using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad interna <see cref="MealItem"/>.
/// La regla de referencia exclusiva (alimento del catálogo XOR alimento personalizado) se
/// refuerza con un CHECK constraint.
/// </summary>
public sealed class MealItemConfiguration : IEntityTypeConfiguration<MealItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MealItem> builder)
    {
        builder.ToTable("meal_items", table =>
        {
            table.HasCheckConstraint("ck_meal_items_quantity", "quantity > 0");
            table.HasCheckConstraint("ck_meal_items_food_xor", "(food_id IS NULL) <> (custom_food_id IS NULL)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("meal_item_id");

        builder.Property(x => x.MealId).HasColumnName("meal_id").IsRequired();
        builder.Property(x => x.FoodId).HasColumnName("food_id");
        builder.Property(x => x.CustomFoodId).HasColumnName("custom_food_id");

        builder.Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasPrecision(8, 2)
            .IsRequired();

        builder.Property(x => x.Unit)
            .HasColumnName("unit")
            .HasMaxLength(20)
            .HasConversion(new SnakeCaseEnumConverter<MeasurementUnit>())
            .IsRequired();

        builder.HasIndex(x => x.MealId).HasDatabaseName("ix_meal_items_meal_id");

        builder.HasOne<FoodItem>()
            .WithMany()
            .HasForeignKey(x => x.FoodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CustomFood>()
            .WithMany()
            .HasForeignKey(x => x.CustomFoodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
