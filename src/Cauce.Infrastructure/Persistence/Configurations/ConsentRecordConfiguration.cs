using Cauce.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="ConsentRecord"/>.
/// La inmutabilidad de las columnas (salvo <c>is_current</c>) se refuerza con
/// triggers de base de datos definidos en la migración.
/// </summary>
public sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("consent_records");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("consent_id");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(x => x.DocumentVersion)
            .HasColumnName("document_version")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.AcceptedAt)
            .HasColumnName("accepted_at")
            .IsRequired();

        builder.Property(x => x.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45);

        builder.Property(x => x.ConsentTextHash)
            .HasColumnName("consent_text_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.IsCurrent)
            .HasColumnName("is_current")
            .IsRequired();

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_consent_records_user_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
