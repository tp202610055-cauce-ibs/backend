using Cauce.Infrastructure.Qa;

namespace Cauce.Api.Commands.Qa;

/// <summary>
/// <c>seed-qa [--manifest &lt;ruta&gt;]</c>: siembra idempotente de las cuentas y los datos de los casos de QA.
/// Exige <c>QA_MODE=1</c> y <c>QA_ALLOW_DESTRUCTIVE=1</c>.
/// </summary>
internal sealed class SeedQaCommand : IOperationCommand
{
    /// <inheritdoc />
    public string Name => "seed-qa";

    /// <inheritdoc />
    public async Task RunAsync(IServiceProvider services, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var parsed = QaArguments.Parse(arguments);
        if (parsed.Targets.Count > 0)
        {
            throw new QaGuardException("seed-qa no recibe casos ni correos; para un caso usa reset-qa.");
        }

        var run = await QaRun.PrepareAsync(services, parsed.ManifestPath, ct).ConfigureAwait(false);
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Cauce.Commands");
        await QaSeeder.SeedAsync(run, logger, ct).ConfigureAwait(false);
    }
}
