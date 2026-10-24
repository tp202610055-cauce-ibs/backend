using System.Globalization;
using System.Text.RegularExpressions;

namespace Cauce.Infrastructure.Qa.Identity;

/// <summary>
/// Comprueba una contraseña contra la política de Keycloak (<c>length(8) and digits(1) ...</c>) antes de escribir nada.
/// Devuelve los nombres de las reglas que no cumple, nunca la contraseña.
/// </summary>
public static partial class QaPasswordPolicy
{
    /// <summary>
    /// Reglas incumplidas; una lista vacía si la contraseña cumple. Las reglas que no se reconocen se ignoran.
    /// </summary>
    /// <param name="password">Contraseña a comprobar.</param>
    /// <param name="policy">Política del realm, o <see langword="null"/> si no tiene.</param>
    /// <param name="usernames">Correos de las cuentas que la usarán, para la regla notUsername.</param>
    public static IReadOnlyList<string> FindViolations(string password, string? policy, IEnumerable<string> usernames)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(usernames);
        var violations = new List<string>();
        if (string.IsNullOrWhiteSpace(policy))
        {
            return violations;
        }

        foreach (Match rule in RulePattern().Matches(policy))
        {
            var name = rule.Groups["name"].Value;
            var argument = rule.Groups["arg"].Value;
            var count = int.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 1;

            var satisfied = name switch
            {
                "length" => password.Length >= count,
                "maxLength" => password.Length <= count,
                "digits" => password.Count(char.IsDigit) >= count,
                "upperCase" => password.Count(char.IsUpper) >= count,
                "lowerCase" => password.Count(char.IsLower) >= count,
                "specialChars" => password.Count(character => !char.IsLetterOrDigit(character)) >= count,
                "notUsername" or "notEmail" => !usernames.Any(username => string.Equals(username, password, StringComparison.OrdinalIgnoreCase)),
                _ => true
            };

            if (!satisfied)
            {
                violations.Add(name);
            }
        }

        return violations;
    }

    [GeneratedRegex(@"(?<name>[A-Za-z]+)\((?<arg>[^)]*)\)")]
    private static partial Regex RulePattern();
}
