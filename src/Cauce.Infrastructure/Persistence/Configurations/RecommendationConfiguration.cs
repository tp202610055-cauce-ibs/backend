using Cauce.Domain.Identity;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.ValueObjects;
using Cauce.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="Recommendation"/> y sus
/// relaciones con sus ítems y su retroalimentación. El puntaje de confianza se persiste como
/// columna decimal directa mediante un conversor de valor.
/// </summary>
public sealed class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Recommendation> builder)
    {
        builder.ToTable("recommendations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("recommendation_id");

        builder.Property(x => x.PatientId).HasColumnName("patient_id").IsRequired();
        builder.Property(x => x.ModelVersionId).HasColumnName("model_version_id").IsRequired();
        builder.Property(x => x.ReviewedByNutritionistId).HasColumnName("reviewed_by_nutritionist_id");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .HasConversion(new SnakeCaseEnumConverter<RecommendationStatus>())
            .IsRequired();

        builder.Property(x => x.ConfidenceScore)
            .HasColumnName("confidence_score")
            .HasColumnType("numeric(4,3)")
            .HasConversion(value => value.Value, value => ConfidenceScore.Create(value))
            .IsRequired();

        builder.Property(x => x.AutoApproved).HasColumnName("auto_approved").IsRequired();
        builder.Property(x => x.NutritionistNote).HasColumnName("nutritionist_note").HasColumnType("text");
        builder.Property(x => x.AiExplanation).HasColumnName("ai_explanation").HasColumnType("text");

        builder.Property(x => x.ExplanationSource)
            .HasColumnName("explanation_source")
            .HasMaxLength(20)
            .HasConversion(new SnakeCaseEnumConverter<ExplanationSource>())
            .IsRequired();

        builder.Property(x => x.GeneratedAt).HasColumnName("generated_at").IsRequired();
        builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(x => x.DeliveredAt).HasColumnName("delivered_at");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");

        builder.HasIndex(x => new { x.PatientId, x.Status, x.GeneratedAt })
            .HasDatabaseName("ix_recommendations_patient_status_generated");

        builder.HasIndex(x => new { x.ReviewedByNutritionistId, x.Status, x.GeneratedAt })
            .HasDatabaseName("ix_recommendations_nutritionist_pending")
            .HasFilter("status = 'pending_review'");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ModelVersion>()
            .WithMany()
            .HasForeignKey(x => x.ModelVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ReviewedByNutritionistId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(item => item.RecommendationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_items");

        builder.HasOne(x => x.Feedback)
            .WithOne()
            .HasForeignKey<RecommendationFeedback>(feedback => feedback.RecommendationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
