using System.Globalization;
using Cauce.Infrastructure.Qa;
using Cauce.Infrastructure.Qa.Manifest;

namespace Cauce.Api.Commands.Qa;

/// <summary>
/// Argumentos de los comandos de inspección: un solo objetivo (caso o correo), <c>--manifest</c>, opciones con valor y
/// banderas. Cualquier otra cosa detiene el comando con el código 2.
/// </summary>
internal sealed record QaInspectionArguments(
    string Target, string? ManifestPath, IReadOnlyDictionary<string, string> Values, IReadOnlySet<string> Flags)
{
    private const string ManifestOption = "--manifest";

    /// <summary>
    /// Lee los argumentos.
    /// </summary>
    /// <exception cref="QaGuardException">Si falta el objetivo, sobra uno o hay una opción desconocida.</exception>
    public static QaInspectionArguments Parse(string command, IReadOnlyList<string> arguments, string[] valueOptions, string[] flags)
    {
        string? manifestPath = null;
        var targets = new List<string>();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var set = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (argument == ManifestOption || valueOptions.Contains(argument, StringComparer.Ordinal))
            {
                if (index + 1 >= arguments.Count)
                {
                    throw new QaGuardException($"Falta el valor después de {argument}.");
                }

                var value = arguments[++index];
                if (argument == ManifestOption)
                {
                    manifestPath = value;
                }
                else
                {
                    values[argument] = value;
                }
            }
            else if (flags.Contains(argument, StringComparer.Ordinal))
            {
                set.Add(argument);
            }
            else if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                throw new QaGuardException($"Opción desconocida: {argument}.");
            }
            else
            {
                targets.Add(argument);
            }
        }

        if (targets.Count != 1)
        {
            throw new QaGuardException($"{command} recibe un caso (CPxxx) o un correo del manifiesto, uno solo.");
        }

        return new QaInspectionArguments(targets[0], manifestPath, values, set);
    }

    /// <summary>
    /// Inicio de la ventana: <c>Nh</c>, <c>Nd</c>, una fecha de Lima (<c>2026-10-24</c>) o fecha y hora de Lima
    /// (<c>2026-10-24T09:30</c>).
    /// </summary>
    /// <exception cref="QaGuardException">Si el valor no tiene ninguno de esos formatos.</exception>
    public static DateTime ParseSince(string value, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(value);
        var amount = value.Length > 1 && int.TryParse(value[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : -1;
        if (amount > 0 && value[^1] is 'h' or 'H')
        {
            return nowUtc.AddHours(-amount);
        }

        if (amount > 0 && value[^1] is 'd' or 'D')
        {
            return nowUtc.AddDays(-amount);
        }

        string[] formats = ["yyyy-MM-dd", "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd HH:mm"];
        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var lima))
        {
            return DateTime.SpecifyKind(lima - QaClock.LimaOffset, DateTimeKind.Utc);
        }

        throw new QaGuardException($"--since no entiende «{value}»: usa 6h, 2d, 2026-10-24 o 2026-10-24T09:30 (hora de Lima).");
    }

    /// <summary>
    /// Entero positivo hasta <paramref name="max"/>.
    /// </summary>
    /// <exception cref="QaGuardException">Si el valor no es un entero en ese rango.</exception>
    public static int ParseLimit(string option, string value, int max)
    {
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var limit) && limit is > 0 && limit <= max)
        {
            return limit;
        }

        throw new QaGuardException($"{option} debe ser un número entre 1 y {max}.");
    }

    /// <summary>
    /// Carga el manifiesto indicado o el que viaja con la API.
    /// </summary>
    public QaManifest LoadManifest() =>
        QaManifestLoader.LoadFile(QaManifestLocator.Resolve(ManifestPath, AppContext.BaseDirectory));
}
