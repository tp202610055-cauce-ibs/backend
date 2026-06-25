using System.Security.Cryptography;
using System.Text;
using Cauce.Application.Common.Interfaces.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Implementación de <see cref="IPasswordResetTokenGenerator"/> que produce un
/// token aleatorio de 32 bytes codificado en base64 URL-safe y su hash SHA-256.
/// </summary>
public sealed class PasswordResetTokenGenerator : IPasswordResetTokenGenerator
{
    private const int TokenSizeInBytes = 32;

    /// <inheritdoc />
    public (string PlainToken, string TokenHash) GeneratePair()
    {
        Span<byte> buffer = stackalloc byte[TokenSizeInBytes];
        RandomNumberGenerator.Fill(buffer);

        var plainToken = WebEncoders.Base64UrlEncode(buffer);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        var tokenHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return (plainToken, tokenHash);
    }
}
