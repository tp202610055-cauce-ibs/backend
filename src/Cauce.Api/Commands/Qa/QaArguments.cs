using Cauce.Infrastructure.Qa;

namespace Cauce.Api.Commands.Qa;

/// <summary>
/// Argumentos comunes de los comandos de QA: <c>--manifest &lt;ruta&gt;</c> y una lista de objetivos (casos o correos).
/// </summary>
internal sealed record QaArguments(string? ManifestPath, IReadOnlyList<string> Targets)
{
    private const string ManifestOption = "--manifest";

    /// <summary>
    /// Lee los argumentos. Una opción desconocida o <c>--manifest</c> sin ruta detienen el comando con el código 2.
    /// </summary>
    /// <exception cref="QaGuardException">Si los argumentos no son válidos.</exception>
    public static QaArguments Parse(IReadOnlyList<string> arguments, params string[] flags)
    {
        string? manifestPath = null;
        var targets = new List<string>();
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (argument == ManifestOption)
            {
                if (index + 1 >= arguments.Count)
                {
                    throw new QaGuardException("Falta la ruta después de --manifest.");
                }

                manifestPath = arguments[++index];
            }
            else if (argument.StartsWith("--", StringComparison.Ordinal) && !flags.Contains(argument, StringComparer.Ordinal))
            {
                throw new QaGuardException($"Opción desconocida: {argument}.");
            }
            else
            {
                targets.Add(argument);
            }
        }

        return new QaArguments(manifestPath, targets);
    }
}
