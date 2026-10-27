using System.Security.Cryptography;
using System.Text;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;

namespace Cauce.Application.Identity.Services;

/// <summary>
/// Estado de un token de restablecimiento al verificarlo.
/// </summary>
public enum PasswordResetTokenState
{
    /// <summary>El token existe, no se usó, no venció y su usuario existe.</summary>
    Valid,

    /// <summary>El token no existe, ya se usó o su usuario no existe.</summary>
    Invalid,

    /// <summary>El token existe y no se usó, pero venció.</summary>
    Expired
}

/// <summary>
/// Resultado de verificar un token de restablecimiento. <c>Token</c> y <c>User</c> solo vienen con
/// <see cref="PasswordResetTokenState.Valid"/>.
/// </summary>
/// <param name="State">Estado del token.</param>
/// <param name="Token">Token encontrado.</param>
/// <param name="User">Usuario dueño del token.</param>
public sealed record PasswordResetTokenVerification(
    PasswordResetTokenState State,
    PasswordResetToken? Token,
    User? User);

/// <summary>
/// Reglas únicas para decidir si un token de restablecimiento sirve. Las usan la validación y la
/// confirmación, para que nunca se contradigan. No modifica el token.
/// </summary>
public static class PasswordResetTokenVerifier
{
    /// <summary>
    /// Verifica el token en claro contra los repositorios.
    /// </summary>
    public static async Task<PasswordResetTokenVerification> VerifyAsync(
        string? plainToken,
        IPasswordResetTokenRepository tokenRepository,
        IUserRepository userRepository,
        DateTime utcNow,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tokenRepository);
        ArgumentNullException.ThrowIfNull(userRepository);

        if (string.IsNullOrWhiteSpace(plainToken))
        {
            return Invalid();
        }

        var token = await tokenRepository.FindByTokenHashAsync(ComputeHash(plainToken), ct).ConfigureAwait(false);
        if (token is null || token.IsUsed)
        {
            return Invalid();
        }

        if (token.IsExpired(utcNow))
        {
            return new PasswordResetTokenVerification(PasswordResetTokenState.Expired, null, null);
        }

        var user = await userRepository.FindByIdAsync(token.UserId, ct).ConfigureAwait(false);
        return user is null
            ? Invalid()
            : new PasswordResetTokenVerification(PasswordResetTokenState.Valid, token, user);
    }

    private static PasswordResetTokenVerification Invalid() =>
        new(PasswordResetTokenState.Invalid, null, null);

    private static string ComputeHash(string plainToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
