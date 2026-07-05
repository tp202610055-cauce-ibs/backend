using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Infrastructure.Persistence.Converters;
using Cauce.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cauce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="User"/>.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("user_id");

        builder.Property(x => x.KeycloakId)
            .HasColumnName("keycloak_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.RoleId)
            .HasColumnName("role_id")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion(new SnakeCaseEnumConverter<UserStatus>())
            .IsRequired();

        builder.Property(x => x.EmailVerified)
            .HasColumnName("email_verified")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(x => x.LastLoginAt)
            .HasColumnName("last_login_at");

        builder.Property(x => x.FailedLoginAttempts)
            .HasColumnName("failed_login_attempts")
            .IsRequired();

        builder.Property(x => x.LockedUntil)
            .HasColumnName("locked_until");

        builder.Property(x => x.FcmToken)
            .HasColumnName("fcm_token")
            .HasMaxLength(500);

        builder.HasIndex(x => x.KeycloakId)
            .IsUnique()
            .HasDatabaseName("ux_users_keycloak_id");

        builder.HasIndex(x => x.Email)
            .IsUnique()
            .HasDatabaseName("ux_users_email");

        builder.HasIndex(x => x.RoleId)
            .HasDatabaseName("ix_users_role_id");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_users_status");

        builder.HasOne<UserRoleEntity>()
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
