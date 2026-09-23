using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Cauce.Domain.Common;
using Cauce.Domain.Identity.Exceptions;

namespace Cauce.Domain.Identity;

/// <summary>
/// Código correlativo legible de un paciente, con formato <c>PAC-0042</c>. Es el seudónimo con el que
/// el paciente aparece en toda salida pensada para investigación (exportación CSV y reporte clínico en
/// PDF), en lugar de su nombre o de su identificador técnico.
///
/// <para>No reemplaza al identificador interno: <c>PatientId</c> (GUID) sigue siendo la clave que usan
/// la app móvil, la sincronización y las claves foráneas. Son dos cosas distintas y con uso distinto:
/// el GUID identifica la fila, el código identifica al sujeto del estudio ante una persona.</para>
/// </summary>
public sealed partial class PatientCode : ValueObject
{
    /// <summary>
    /// Prefijo fijo de todo código de paciente.
    /// </summary>
    public const string Prefix = "PAC-";

    /// <summary>
    /// Cantidad mínima de dígitos del correlativo. Un correlativo mayor que <c>9999</c> simplemente
    /// usa más dígitos (<c>PAC-10000</c>); no se trunca ni se reinicia.
    /// </summary>
    public const int MinDigits = 4;

    /// <summary>
    /// Representación canónica del código, por ejemplo <c>PAC-0042</c>.
    /// </summary>
    public string Value { get; }

    private PatientCode(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Construye el código canónico de un correlativo.
    /// </summary>
    /// <param name="correlative">Correlativo positivo asignado al paciente.</param>
    /// <returns>El código de paciente.</returns>
    /// <exception cref="InvalidPatientCodeException">Si el correlativo no es positivo.</exception>
    public static PatientCode FromCorrelative(long correlative)
    {
        if (correlative <= 0)
        {
            throw new InvalidPatientCodeException(correlative.ToString(CultureInfo.InvariantCulture));
        }

        var digits = correlative.ToString(CultureInfo.InvariantCulture).PadLeft(MinDigits, '0');
        return new PatientCode(Prefix + digits);
    }

    /// <summary>
    /// Interpreta un código ya existente, validando su formato.
    /// </summary>
    /// <param name="value">Valor a interpretar.</param>
    /// <returns>El código de paciente.</returns>
    /// <exception cref="InvalidPatientCodeException">Si el valor no respeta el formato canónico.</exception>
    public static PatientCode Parse(string? value)
    {
        return TryParse(value, out var code)
            ? code
            : throw new InvalidPatientCodeException(value);
    }

    /// <summary>
    /// Intenta interpretar un código sin lanzar excepciones.
    /// </summary>
    /// <param name="value">Valor a interpretar.</param>
    /// <param name="code">Código resultante si el valor es válido.</param>
    /// <returns><see langword="true"/> si el valor respeta el formato canónico.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out PatientCode? code)
    {
        code = null;
        if (string.IsNullOrWhiteSpace(value) || !CanonicalFormat().IsMatch(value))
        {
            return false;
        }

        // El formato canónico es el que produce FromCorrelative: se exige el viaje de ida y vuelta
        // para que un mismo correlativo no admita dos escrituras (PAC-0042 y PAC-00042) y para
        // descartar el correlativo cero.
        if (!long.TryParse(value.AsSpan(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var correlative)
            || correlative <= 0
            || !string.Equals(FromCorrelative(correlative).Value, value, StringComparison.Ordinal))
        {
            return false;
        }

        code = new PatientCode(value);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    // Filtro de forma: prefijo y al menos cuatro dígitos. La canonicidad del relleno con ceros la
    // decide TryParse comparando contra FromCorrelative.
    [GeneratedRegex(@"^PAC-[0-9]{4,}$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalFormat();
}
