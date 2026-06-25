using System.Security.Cryptography;
using Cauce.Application.Common.Interfaces.Identity;

namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Implementación de <see cref="ITemporaryPasswordGenerator"/> que genera
/// contraseñas temporales de 12 caracteres garantizando al menos una mayúscula,
/// una minúscula, un dígito y un símbolo, mezclados de forma criptográficamente
/// aleatoria.
/// </summary>
public sealed class TemporaryPasswordGenerator : ITemporaryPasswordGenerator
{
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%*?";
    private const int TotalLength = 12;

    /// <inheritdoc />
    public string Generate()
    {
        var all = Uppercase + Lowercase + Digits + Symbols;

        var characters = new List<char>(TotalLength)
        {
            Uppercase[RandomNumberGenerator.GetInt32(Uppercase.Length)],
            Lowercase[RandomNumberGenerator.GetInt32(Lowercase.Length)],
            Digits[RandomNumberGenerator.GetInt32(Digits.Length)],
            Symbols[RandomNumberGenerator.GetInt32(Symbols.Length)]
        };

        while (characters.Count < TotalLength)
        {
            characters.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        }

        Shuffle(characters);
        return new string(characters.ToArray());
    }

    private static void Shuffle(IList<char> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
