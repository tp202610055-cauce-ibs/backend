using Cauce.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para el catálogo de roles
/// (<see cref="UserRoleEntity"/>).
/// </summary>
public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRoleEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserRoleEntity> builder)
    {
        builder.ToTable("user_roles");

        builder.HasKey(x => x.RoleId);
        builder.Property(x => x.RoleId)
            .HasColumnName("role_id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.RoleName)
            .HasColumnName("role_name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(255);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(x => x.RoleName)
            .IsUnique()
            .HasDatabaseName("ux_user_roles_role_name");
    }
}
