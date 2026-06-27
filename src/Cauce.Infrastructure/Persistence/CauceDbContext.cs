using Cauce.Domain.Auditing;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence;

/// <summary>
/// Contexto principal de Entity Framework Core para la base de datos de Cauce.
/// Actúa como unidad de trabajo. Las configuraciones de cada entidad se aplican
/// automáticamente desde el ensamblado de infraestructura. Los <c>DbSet</c> de
/// cada módulo de dominio se incorporan al implementar el módulo correspondiente.
/// </summary>
public sealed class CauceDbContext : DbContext
{
    /// <summary>
    /// Inicializa el contexto con las opciones especificadas.
    /// </summary>
    /// <param name="options">Opciones de configuración del contexto.</param>
    public CauceDbContext(DbContextOptions<CauceDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Bitácora de auditoría inmutable, transversal a todos los módulos.
    /// </summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>
    /// Cuentas de usuario del sistema.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Catálogo de roles de usuario.
    /// </summary>
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();

    /// <summary>
    /// Códigos de invitación generados por nutricionistas.
    /// </summary>
    public DbSet<InvitationCode> InvitationCodes => Set<InvitationCode>();

    /// <summary>
    /// Tokens de restablecimiento de contraseña.
    /// </summary>
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    /// <summary>
    /// Registros de consentimiento informado.
    /// </summary>
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();

    /// <summary>
    /// Perfiles clínicos de pacientes.
    /// </summary>
    public DbSet<PatientProfile> PatientProfiles => Set<PatientProfile>();

    /// <summary>
    /// Catálogo de alergias e intolerancias.
    /// </summary>
    public DbSet<Allergy> Allergies => Set<Allergy>();

    /// <summary>
    /// Declaraciones de alergia de los pacientes.
    /// </summary>
    public DbSet<PatientAllergy> PatientAllergies => Set<PatientAllergy>();

    /// <summary>
    /// Asignaciones nutricionista-paciente.
    /// </summary>
    public DbSet<NutritionistPatient> NutritionistPatients => Set<NutritionistPatient>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CauceDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
