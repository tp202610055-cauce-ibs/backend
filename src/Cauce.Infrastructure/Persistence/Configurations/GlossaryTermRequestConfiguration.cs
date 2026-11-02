using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using Cauce.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de <see cref="GlossaryTermRequest"/> en la tabla <c>glossary_term_requests</c>.
/// </summary>
public sealed class GlossaryTermRequestConfiguration : IEntityTypeConfiguration<GlossaryTermRequest>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GlossaryTermRequest> builder)
    {
        builder.ToTable("glossary_term_requests");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("glossary_term_request_id");

        builder.Property(x => x.NutritionistId).HasColumnName("nutritionist_id").IsRequired();
        builder.Property(x => x.Term).HasColumnName("term").HasMaxLength(GlossaryTermRequest.MaxTermLength).IsRequired();
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion(new SnakeCaseEnumConverter<GlossaryTermRequestStatus>())
            .IsRequired();
        builder.Property(x => x.RequestedAt).HasColumnName("requested_at").IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.NutritionistId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.Status, x.RequestedAt })
            .HasDatabaseName("ix_glossary_term_requests_status_requested");

        builder.HasIndex(x => x.NutritionistId)
            .HasDatabaseName("ix_glossary_term_requests_nutritionist");
    }
}
