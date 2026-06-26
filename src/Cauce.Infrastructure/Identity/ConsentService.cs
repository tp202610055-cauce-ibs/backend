using System.Security.Cryptography;
using System.Text;
using Cauce.Application.Common.Interfaces.Identity;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Implementación de <see cref="IConsentService"/> que expone el documento de
/// consentimiento vigente y cachea su hash SHA-256, calculado una sola vez en la
/// construcción.
/// </summary>
public sealed class ConsentService : IConsentService
{
    private readonly string _currentVersion;
    private readonly string _currentText;
    private readonly string _currentTextHash;

    /// <summary>
    /// Inicializa el servicio leyendo el documento vigente de la configuración y
    /// precalculando su hash.
    /// </summary>
    /// <param name="options">Opciones del documento de consentimiento.</param>
    public ConsentService(IOptions<ConsentDocumentOptions> options)
    {
        _currentVersion = options.Value.CurrentVersion;
        _currentText = options.Value.Text;
        _currentTextHash = ComputeHash(_currentText);
    }

    /// <inheritdoc />
    public string GetCurrentVersion() => _currentVersion;

    /// <inheritdoc />
    public string GetCurrentText() => _currentText;

    /// <inheritdoc />
    public string GetCurrentTextHash() => _currentTextHash;

    /// <inheritdoc />
    public bool VerifyHash(string documentVersion, string clientProvidedHash)
    {
        return string.Equals(documentVersion, _currentVersion, StringComparison.Ordinal)
            && string.Equals(clientProvidedHash, _currentTextHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
