using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="InvitationCode"/>.
/// </summary>
public sealed class InvitationCodeConfiguration : IEntityTypeConfiguration<InvitationCode>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InvitationCode> builder)
    {
        builder.ToTable("invitation_codes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("code_id");

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.NutritionistId)
            .HasColumnName("nutritionist_id")
            .IsRequired();

        builder.Property(x => x.UsedByPatientId)
            .HasColumnName("used_by_patient_id");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion(new SnakeCaseEnumConverter<InvitationStatus>())
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(x => x.UsedAt)
            .HasColumnName("used_at");

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("ux_invitation_codes_code");

        builder.HasIndex(x => x.NutritionistId)
            .HasDatabaseName("ix_invitation_codes_nutritionist_id");

        builder.HasIndex(x => new { x.Status, x.ExpiresAt })
            .HasDatabaseName("ix_invitation_codes_status_expires_at");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.NutritionistId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UsedByPatientId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
