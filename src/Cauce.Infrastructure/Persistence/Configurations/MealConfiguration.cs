using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using Cauce.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="Meal"/> y su
/// colección de ítems encapsulada.
/// </summary>
public sealed class MealConfiguration : IEntityTypeConfiguration<Meal>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Meal> builder)
    {
        builder.ToTable("meals");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("meal_id");

        builder.Property(x => x.ClientGuid).HasColumnName("client_guid").IsRequired();
        builder.Property(x => x.PatientId).HasColumnName("patient_id").IsRequired();

        builder.Property(x => x.MealTime)
            .HasColumnName("meal_time")
            .HasMaxLength(20)
            .HasConversion(new SnakeCaseEnumConverter<MealTime>())
            .IsRequired();

        builder.Property(x => x.ConsumedAt).HasColumnName("consumed_at").IsRequired();

        builder.Property(x => x.SyncStatus)
            .HasColumnName("sync_status")
            .HasMaxLength(20)
            .HasConversion(new SnakeCaseEnumConverter<SyncStatus>())
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.ClientCreatedAt).HasColumnName("client_created_at").IsRequired();

        builder.HasIndex(x => x.ClientGuid).IsUnique().HasDatabaseName("ux_meals_client_guid");
        builder.HasIndex(x => x.PatientId).HasDatabaseName("ix_meals_patient_id");
        builder.HasIndex(x => x.ClientCreatedAt).HasDatabaseName("ix_meals_client_created_at");
        builder.HasIndex(x => new { x.PatientId, x.ClientCreatedAt }).HasDatabaseName("ix_meals_patient_clientcrat");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.MealId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_items");
    }
}
