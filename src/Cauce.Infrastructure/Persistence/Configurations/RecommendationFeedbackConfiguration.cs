using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad interna
/// <see cref="RecommendationFeedback"/>. La relación uno a uno con la recomendación garantiza
/// que exista a lo sumo una retroalimentación por recomendación.
/// </summary>
public sealed class RecommendationFeedbackConfiguration : IEntityTypeConfiguration<RecommendationFeedback>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RecommendationFeedback> builder)
    {
        builder.ToTable("recommendation_feedback");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("feedback_id");

        builder.Property(x => x.RecommendationId).HasColumnName("recommendation_id").IsRequired();
        builder.Property(x => x.WasApplied).HasColumnName("was_applied").IsRequired();

        builder.Property(x => x.Outcome)
            .HasColumnName("outcome")
            .HasMaxLength(20)
            .HasConversion(new SnakeCaseEnumConverter<FeedbackOutcome>())
            .IsRequired();

        builder.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(500);

        builder.Property(x => x.SyncStatus)
            .HasColumnName("sync_status")
            .HasMaxLength(20)
            .HasConversion(new SnakeCaseEnumConverter<SyncStatus>())
            .IsRequired();

        builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at").IsRequired();

        builder.HasIndex(x => x.RecommendationId).IsUnique().HasDatabaseName("ux_recommendation_feedback_recommendation_id");
    }
}
