using Cauce.Domain.Identity;
using Cauce.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeder idempotente de las versiones del documento de consentimiento informado.
/// </summary>
/// <remarks>
/// <para>
/// Siembra la versión configurada en <c>appsettings.Consent</c> si todavía no existe. El texto
/// se copia <b>byte a byte</b>: el hash resultante tiene que coincidir con el que devuelve
/// <c>GET /consent/current</c>, o el registro de pacientes empezaría a fallar con
/// <c>consent_text_mismatch</c>.
/// </para>
/// <para>
/// <b>No actualiza una versión existente.</b> Si el texto de configuración cambió sin cambiar el
/// número de versión, el seeder deja la fila como está y la comprobación de arranque emite la
/// advertencia. Sobrescribirla rompería la correspondencia con los <c>consent_records</c> que ya
/// la referencian.
/// </para>
/// </remarks>
public sealed class ConsentDocumentsSeeder
{
    private readonly CauceDbContext _context;
    private readonly ConsentDocumentOptions _options;
    private readonly ILogger<ConsentDocumentsSeeder> _logger;

    /// <summary>
    /// Inicializa el seeder con sus dependencias.
    /// </summary>
    /// <param name="context">Contexto de base de datos.</param>
    /// <param name="options">Configuración del consentimiento vigente.</param>
    /// <param name="logger">Logger de la categoría del seeder.</param>
    public ConsentDocumentsSeeder(
        CauceDbContext context,
        IOptions<ConsentDocumentOptions> options,
        ILogger<ConsentDocumentsSeeder> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Siembra la versión vigente de forma idempotente.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var version = _options.CurrentVersion;

        var exists = await _context.Set<ConsentDocument>()
            .AnyAsync(x => x.Version == version, ct)
            .ConfigureAwait(false);

        if (exists)
        {
            return;
        }

        // Cualquier versión anterior deja de ser la vigente antes de publicar la nueva.
        var previous = await _context.Set<ConsentDocument>()
            .Where(x => x.IsCurrent)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var document in previous)
        {
            document.Supersede();
        }

        _context.Set<ConsentDocument>().Add(
            ConsentDocument.Publish(Guid.NewGuid(), version, _options.Text, DateTime.UtcNow));

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Seeded consent document version {Version}.", version);
    }
}
