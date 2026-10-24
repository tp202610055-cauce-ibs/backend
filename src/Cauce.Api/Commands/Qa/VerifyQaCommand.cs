using Cauce.Infrastructure.Qa;
using Cauce.Infrastructure.Qa.Manifest;

namespace Cauce.Api.Commands.Qa;

/// <summary>
/// <c>verify-qa [CPxxx ... | --all] [--manifest &lt;ruta&gt;]</c>: comprueba, sin escribir nada, que cada caso esté listo.
/// Imprime una línea por caso y un resumen; termina con 1 si alguno falla. Exige <c>QA_MODE=1</c>.
/// </summary>
internal sealed class VerifyQaCommand : IOperationCommand
{
    private const string AllFlag = "--all";

    /// <inheritdoc />
    public string Name => "verify-qa";

    /// <inheritdoc />
    public async Task RunAsync(IServiceProvider services, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        new QaSettings(services.GetRequiredService<IConfiguration>()).EnsureQaMode();
        var parsed = QaArguments.Parse(arguments, AllFlag);
        var targets = parsed.Targets.Where(target => target != AllFlag).ToList();
        if (targets.Count > 0 && arguments.Contains(AllFlag))
        {
            throw new QaGuardException("Usa --all o una lista de casos, no ambos.");
        }

        var manifest = QaManifestLoader.LoadFile(QaManifestLocator.Resolve(parsed.ManifestPath, AppContext.BaseDirectory));
        var unknown = targets.Where(target => manifest.FindCase(target) is null).Select(target => $"Caso desconocido: {target}.").ToList();
        if (unknown.Count > 0)
        {
            throw new QaManifestException(unknown);
        }

        var cases = targets.Count == 0
            ? manifest.Cases
            : manifest.Cases.Where(testCase => targets.Contains(testCase.Cp, StringComparer.OrdinalIgnoreCase)).ToList();

        var verifier = ActivatorUtilities.CreateInstance<QaVerifier>(services);
        var verdicts = await verifier.VerifyAsync(manifest, cases, ct).ConfigureAwait(false);

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Cauce.Commands");
        foreach (var verdict in verdicts)
        {
            if (verdict.Ok)
            {
                logger.LogInformation("{Cp} OK", verdict.Cp);
            }
            else
            {
                logger.LogWarning("{Cp} FALLA: {Problems}", verdict.Cp, string.Join("; ", verdict.Problems));
            }
        }

        var failed = verdicts.Count(verdict => !verdict.Ok);
        logger.LogInformation("verify-qa: {Ok} OK, {Failed} FALLA de {Total} caso(s).", verdicts.Count - failed, failed, verdicts.Count);
        if (failed > 0)
        {
            throw new QaFailureException($"{failed} de {verdicts.Count} caso(s) con falla.");
        }
    }
}
