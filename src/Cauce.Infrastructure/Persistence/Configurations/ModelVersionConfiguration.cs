using Cauce.Domain.Recommendations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="ModelVersion"/>. Un índice
/// único filtrado garantiza, a nivel de base de datos, que a lo sumo una versión esté activa.
/// </summary>
public sealed class ModelVersionConfiguration : IEntityTypeConfiguration<ModelVersion>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ModelVersion> builder)
    {
        builder.ToTable("model_versions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("version_id");

        builder.Property(x => x.VersionName).HasColumnName("version_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ModelHash).HasColumnName("model_hash").HasMaxLength(128).IsRequired();
        builder.Property(x => x.TrainingDatasetSize).HasColumnName("training_dataset_size");
        builder.Property(x => x.PerformanceMetricsJson).HasColumnName("performance_metrics").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DeployedAt).HasColumnName("deployed_at").IsRequired();
        builder.Property(x => x.DeployedBy).HasColumnName("deployed_by").HasMaxLength(100).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(x => x.VersionName).IsUnique().HasDatabaseName("ux_model_versions_version_name");
        builder.HasIndex(x => x.ModelHash).IsUnique().HasDatabaseName("ux_model_versions_model_hash");

        builder.HasIndex(x => x.IsActive)
            .IsUnique()
            .HasDatabaseName("ix_model_versions_only_one_active")
            .HasFilter("is_active = true");
    }
}
