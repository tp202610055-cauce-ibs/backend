using Cauce.Domain.ClinicalRegistry;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad interna
/// <see cref="CustomFoodIngredient"/>. La relación con su raíz de agregado se configura
/// desde <see cref="CustomFoodConfiguration"/>.
/// </summary>
public sealed class CustomFoodIngredientConfiguration : IEntityTypeConfiguration<CustomFoodIngredient>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CustomFoodIngredient> builder)
    {
        builder.ToTable("custom_food_ingredients", table =>
        {
            table.HasCheckConstraint("ck_custom_food_ingredients_proportion", "proportion_grams > 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("ingredient_id");

        builder.Property(x => x.CustomFoodId).HasColumnName("custom_food_id").IsRequired();
        builder.Property(x => x.FoodId).HasColumnName("food_id").IsRequired();

        builder.Property(x => x.ProportionGrams)
            .HasColumnName("proportion_grams")
            .HasPrecision(6, 2)
            .IsRequired();

        builder.HasIndex(x => new { x.CustomFoodId, x.FoodId })
            .IsUnique()
            .HasDatabaseName("uq_ingredient_per_custom_food");

        builder.HasOne<FoodItem>()
            .WithMany()
            .HasForeignKey(x => x.FoodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
