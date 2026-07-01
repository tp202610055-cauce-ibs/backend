using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad interna <see cref="RecommendationItem"/>.
/// Las claves foráneas al catálogo de alimentos (alimento principal y sustituto) usan
/// comportamiento de borrado restringido.
/// </summary>
public sealed class RecommendationItemConfiguration : IEntityTypeConfiguration<RecommendationItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RecommendationItem> builder)
    {
        builder.ToTable("recommendation_items");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("recommendation_item_id");

        builder.Property(x => x.RecommendationId).HasColumnName("recommendation_id").IsRequired();
        builder.Property(x => x.FoodId).HasColumnName("food_id").IsRequired();
        builder.Property(x => x.SubstituteFoodId).HasColumnName("substitute_food_id");

        builder.Property(x => x.ActionType)
            .HasColumnName("action_type")
            .HasMaxLength(20)
            .HasConversion(new SnakeCaseEnumConverter<ActionType>())
            .IsRequired();

        builder.Property(x => x.Reasoning).HasColumnName("reasoning").HasColumnType("text");

        builder.HasIndex(x => x.RecommendationId).HasDatabaseName("ix_recommendation_items_recommendation_id");

        builder.HasOne<FoodItem>()
            .WithMany()
            .HasForeignKey(x => x.FoodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<FoodItem>()
            .WithMany()
            .HasForeignKey(x => x.SubstituteFoodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
