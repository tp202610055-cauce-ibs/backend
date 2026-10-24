using Cauce.Infrastructure.Qa;

namespace Cauce.Api.Commands.Qa;

/// <summary>
/// <c>reset-qa &lt;CPxxx | correo&gt; [...] [--manifest &lt;ruta&gt;]</c>: rearma los datos de uno o más casos o cuentas y
/// los vuelve a anclar al reloj. Exige <c>QA_MODE=1</c> y <c>QA_ALLOW_DESTRUCTIVE=1</c>.
/// </summary>
internal sealed class ResetQaCommand : IOperationCommand
{
    /// <inheritdoc />
    public string Name => "reset-qa";

    /// <inheritdoc />
    public async Task RunAsync(IServiceProvider services, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var parsed = QaArguments.Parse(arguments);
        var run = await QaRun.PrepareAsync(services, parsed.ManifestPath, ct).ConfigureAwait(false);
        QaResetter.EnsureKnownTargets(run.Manifest, parsed.Targets);

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Cauce.Commands");
        await QaResetter.ResetAsync(run, parsed.Targets, logger, ct).ConfigureAwait(false);
    }
}
