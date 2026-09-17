using Cauce.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de persistencia de <see cref="ConsentDocument"/>.
/// </summary>
public sealed class ConsentDocumentConfiguration : IEntityTypeConfiguration<ConsentDocument>
{
    /// <summary>
    /// Configura el mapeo de la entidad.
    /// </summary>
    /// <param name="builder">Constructor del tipo de entidad.</param>
    public void Configure(EntityTypeBuilder<ConsentDocument> builder)
    {
        builder.ToTable("consent_documents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("consent_document_id");

        builder.Property(x => x.Version)
            .HasColumnName("version")
            .HasMaxLength(20)
            .IsRequired();

        // Sin longitud máxima: el texto definitivo lo provee el equipo clínico del
        // Complejo Hospitalario Guillermo Kaelín y no hay razón para acotarlo.
        builder.Property(x => x.Text)
            .HasColumnName("text")
            .IsRequired();

        builder.Property(x => x.TextHash)
            .HasColumnName("text_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.PublishedAt)
            .HasColumnName("published_at")
            .IsRequired();

        builder.Property(x => x.IsCurrent)
            .HasColumnName("is_current")
            .IsRequired();

        // La versión es la clave con la que la referencian los consent_records, así que
        // no puede repetirse: dos filas con la misma versión dejarían ambiguo qué texto
        // aceptó el paciente, que es justo lo que esta tabla viene a resolver.
        builder.HasIndex(x => x.Version)
            .IsUnique()
            .HasDatabaseName("ix_consent_documents_version");
    }
}
