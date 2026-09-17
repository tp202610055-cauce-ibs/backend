using Cauce.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Persistence.Seeders;

// Nota de nombres: `Cauce.Infrastructure.Identity.ConsentDocument` es la plantilla de
// QuestPDF y no tiene relacion con la entidad de dominio del mismo nombre. Para que
// `ConsentDocument` aqui signifique siempre la entidad, el tipo de opciones se califica
// entero en vez de importar ese namespace.

/// <summary>
/// Publica la versión configurada del consentimiento y verifica que no haya divergido.
/// </summary>
public static class ConsentDocumentStartup
{
    /// <summary>
    /// Asegura la versión vigente en <c>consent_documents</c> y comprueba su integridad.
    /// </summary>
    /// <param name="serviceProvider">Proveedor de servicios de la aplicación.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    /// <remarks>
    /// <para>
    /// Corre en <b>todos los ambientes</b>, a diferencia del resto de los seeders. El texto del
    /// consentimiento no es dato de prueba: es la configuración que hace falta para que el
    /// comprobante en PDF reproduzca lo que el paciente aceptó. Sin esta fila, el endpoint de
    /// descarga no tiene de dónde sacar el texto. Tampoco comparte el riesgo de
    /// <c>DemoPatientSeeder</c>, que la convención gatea a Development porque provisiona un
    /// usuario con contraseña; acá solo se materializa configuración ya presente en el archivo.
    /// </para>
    /// <para>
    /// La comprobación de integridad existe por un caso real observado: en la base de desarrollo
    /// aparecieron dos <c>consent_records</c> con <c>document_version</c> "1.0" y hashes
    /// distintos, lo que solo ocurre si el texto cambió sin que cambiara la versión. Cuando eso
    /// pasa, el registro viejo queda irreproducible. La advertencia no tumba el arranque: avisa
    /// para que alguien publique una versión nueva en vez de seguir editando la misma.
    /// </para>
    /// </remarks>
    public static async Task EnsureConsentDocumentAsync(
        this IServiceProvider serviceProvider,
        CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("ConsentDocumentStartup");

        try
        {
            var context = services.GetRequiredService<CauceDbContext>();
            var options = services.GetRequiredService<IOptions<Cauce.Infrastructure.Identity.ConsentDocumentOptions>>().Value;

            await services.GetRequiredService<ConsentDocumentsSeeder>()
                .SeedAsync(ct)
                .ConfigureAwait(false);

            var current = await context.Set<ConsentDocument>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IsCurrent, ct)
                .ConfigureAwait(false);

            if (current is null)
            {
                logger.LogWarning(
                    "No hay ninguna version de consentimiento marcada como vigente. "
                    + "El comprobante en PDF no podra generarse.");
                return;
            }

            var configuredHash = ConsentDocument.ComputeHash(options.Text);
            if (string.Equals(configuredHash, current.TextHash, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Solo hashes y versiones: el texto del consentimiento no se loggea.
            logger.LogWarning(
                "El texto de appsettings.Consent.Text no coincide con la version {Version} "
                + "guardada en consent_documents. Configurado {ConfiguredHash}, almacenado "
                + "{StoredHash}. Cambiar la redaccion exige publicar una version nueva, no "
                + "editar la vigente: los consent_records que referencian {Version} quedan "
                + "irreproducibles.",
                current.Version,
                configuredHash,
                current.TextHash,
                current.Version);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "No se pudo verificar el documento de consentimiento. La aplicacion continua.");
        }
    }
}
