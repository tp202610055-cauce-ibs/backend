using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="FoodItem"/>. Los
/// valores nutricionales no negativos se refuerzan con CHECK constraints a nivel de base
/// de datos.
/// </summary>
public sealed class FoodItemConfiguration : IEntityTypeConfiguration<FoodItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FoodItem> builder)
    {
        builder.ToTable("food_items", table =>
        {
            table.HasCheckConstraint("ck_food_items_calories", "calories_per_100g >= 0");
            table.HasCheckConstraint("ck_food_items_protein", "protein_g_per_100g >= 0");
            table.HasCheckConstraint("ck_food_items_carbs", "carbs_g_per_100g >= 0");
            table.HasCheckConstraint("ck_food_items_fat", "fat_g_per_100g >= 0");
            table.HasCheckConstraint("ck_food_items_fiber", "fiber_g_per_100g >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("food_id");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasColumnName("category")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CaloriesPer100g).HasColumnName("calories_per_100g").HasPrecision(6, 2).IsRequired();
        builder.Property(x => x.ProteinGPer100g).HasColumnName("protein_g_per_100g").HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.CarbsGPer100g).HasColumnName("carbs_g_per_100g").HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.FatGPer100g).HasColumnName("fat_g_per_100g").HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.FiberGPer100g).HasColumnName("fiber_g_per_100g").HasPrecision(5, 2).IsRequired();

        builder.Property(x => x.FodmapLevel)
            .HasColumnName("fodmap_level")
            .HasMaxLength(10)
            .HasConversion(new SnakeCaseEnumConverter<FodmapLevel>())
            .IsRequired();

        builder.Property(x => x.FodmapTags).HasColumnName("fodmap_tags").HasColumnType("text");
        builder.Property(x => x.IsPeruvian).HasColumnName("is_peruvian").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("ux_food_items_name");
        builder.HasIndex(x => x.Category).HasDatabaseName("ix_food_items_category");
        builder.HasIndex(x => x.FodmapLevel).HasDatabaseName("ix_food_items_fodmap");
        builder.HasIndex(x => x.IsActive).HasDatabaseName("ix_food_items_is_active");
    }
}
