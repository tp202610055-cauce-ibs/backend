using Cauce.Domain.Auditing;
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

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CauceDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
