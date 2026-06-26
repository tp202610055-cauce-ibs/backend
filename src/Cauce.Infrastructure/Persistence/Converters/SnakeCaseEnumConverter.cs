using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Cauce.Infrastructure.Persistence.Converters;

/// <summary>
/// Conversor de EF Core que persiste un enum como texto en snake_case lowercase
/// (por ejemplo, <c>PendingActivation</c> ↔ <c>"pending_activation"</c>).
/// </summary>
/// <typeparam name="TEnum">Tipo del enum a convertir.</typeparam>
public sealed class SnakeCaseEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    /// <summary>
    /// Inicializa el conversor.
    /// </summary>
    public SnakeCaseEnumConverter()
        : base(
            value => ToSnakeCase(value.ToString()),
            value => Enum.Parse<TEnum>(ToPascalCase(value)))
    {
    }

    private static string ToSnakeCase(string value)
    {
        var builder = new StringBuilder(value.Length + 5);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (char.IsUpper(current) && i > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }

    private static string ToPascalCase(string value)
    {
        var segments = value.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var builder = new StringBuilder(value.Length);
        foreach (var segment in segments)
        {
            builder.Append(char.ToUpperInvariant(segment[0]));
            if (segment.Length > 1)
            {
                builder.Append(segment[1..]);
            }
        }

        return builder.ToString();
    }
}
