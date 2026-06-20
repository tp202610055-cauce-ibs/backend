using Cauce.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.Infrastructure.Persistence;

/// <summary>
/// Contexto principal de EF Core. Hereda de IdentityDbContext para integrar
/// ASP.NET Core Identity (gestión de credenciales temporal por adenda DEC-009).
/// El sistema de roles M:M de Identity NO se utiliza funcionalmente; el diseño
/// OE2 define una relación 1:M usuario-rol mediante la tabla user_roles propia.
/// Las tablas aspnet_roles y aspnet_user_roles existirán pero permanecerán
/// vacías hasta su eliminación en v0.2.0 (reincorporación de Keycloak).
/// </summary>
public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    /// <summary>Catálogo de roles del sistema (1=patient, 2=nutritionist).</summary>
    public DbSet<UserRole> AppUserRoles { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // --- Tabla users (mapeo desde IdentityUser<Guid> + campos del diseño OE2) ---
        builder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            // Propiedades heredadas de IdentityUser<Guid> mapeadas a snake_case
            entity.Property(u => u.Id).HasColumnName("user_id");
            entity.Property(u => u.UserName).HasColumnName("username").HasMaxLength(150);
            entity.Property(u => u.NormalizedUserName).HasColumnName("normalized_username").HasMaxLength(150);
            entity.Property(u => u.Email).HasColumnName("email").HasMaxLength(150);
            entity.Property(u => u.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(150);
            entity.Property(u => u.EmailConfirmed).HasColumnName("email_verified");
            entity.Property(u => u.PasswordHash).HasColumnName("password_hash");
            entity.Property(u => u.SecurityStamp).HasColumnName("security_stamp");
            entity.Property(u => u.ConcurrencyStamp).HasColumnName("concurrency_stamp");
            entity.Property(u => u.PhoneNumber).HasColumnName("phone_number");
            entity.Property(u => u.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
            entity.Property(u => u.TwoFactorEnabled).HasColumnName("two_factor_enabled");
            entity.Property(u => u.LockoutEnd).HasColumnName("locked_until");
            entity.Property(u => u.LockoutEnabled).HasColumnName("lockout_enabled");
            entity.Property(u => u.AccessFailedCount).HasColumnName("failed_login_attempts");

            // Campos propios del diseño OE2
            entity.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(150).IsRequired();
            entity.Property(u => u.RoleId).HasColumnName("role_id");
            entity.Property(u => u.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("pending_activation");
            entity.Property(u => u.KeycloakId).HasColumnName("keycloak_id").HasMaxLength(100);
            entity.Property(u => u.CreatedAt).HasColumnName("created_at");
            entity.Property(u => u.UpdatedAt).HasColumnName("updated_at");
            entity.Property(u => u.LastLoginAt).HasColumnName("last_login_at");

            // Relación con UserRole (ON DELETE RESTRICT como en el diseño)
            entity.HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // --- Tabla user_roles ---
        builder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(r => r.RoleId);
            entity.Property(r => r.RoleId).HasColumnName("role_id").ValueGeneratedOnAdd();
            entity.Property(r => r.RoleName).HasColumnName("role_name").HasMaxLength(50).IsRequired();
            entity.HasIndex(r => r.RoleName).IsUnique();
            entity.Property(r => r.Description).HasColumnName("description").HasMaxLength(255);
            entity.Property(r => r.IsActive).HasColumnName("is_active").HasDefaultValue(true);

            // Seed: roles predefinidos del diseño OE2
            entity.HasData(
                new UserRole { RoleId = 1, RoleName = "patient", Description = "Paciente con Síndrome de Intestino Irritable", IsActive = true },
                new UserRole { RoleId = 2, RoleName = "nutritionist", Description = "Dietista-Nutricionista", IsActive = true }
            );
        });

        // --- Tablas auxiliares de Identity (renombradas a snake_case) ---
        // Existen pero NO se utilizan funcionalmente. Se eliminan en v0.2.0.
        builder.Entity<IdentityRole<Guid>>().ToTable("aspnet_roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("aspnet_user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("aspnet_user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("aspnet_user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("aspnet_user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("aspnet_role_claims");
    }
}