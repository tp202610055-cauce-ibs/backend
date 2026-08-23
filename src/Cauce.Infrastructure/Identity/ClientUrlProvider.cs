using Cauce.Application.Common.Identity;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Infrastructure.Email;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Implementación de <see cref="IClientUrlProvider"/> que construye enlaces de cara al cliente a
/// partir de las URL base configuradas en <see cref="EmailOptions"/>, eligiendo el destino según el
/// cliente OIDC que originó la solicitud.
/// </summary>
public sealed class ClientUrlProvider : IClientUrlProvider
{
    private const string PasswordResetPath = "auth/password-reset";

    private readonly string _mobileBaseUrl;
    private readonly string _portalBaseUrl;

    /// <summary>
    /// Inicializa el proveedor con las URL base de la app móvil y del portal web.
    /// </summary>
    /// <param name="emailOptions">Opciones de correo que contienen ambas URL base.</param>
    public ClientUrlProvider(IOptions<EmailOptions> emailOptions)
    {
        _mobileBaseUrl = NormalizeBaseUrl(emailOptions.Value.MobileAppBaseUrl);
        _portalBaseUrl = NormalizeBaseUrl(emailOptions.Value.PortalAppBaseUrl);
    }

    /// <inheritdoc />
    public string BuildPasswordResetLink(string plainToken, string clientId)
    {
        var baseUrl = clientId switch
        {
            OidcClients.Mobile => _mobileBaseUrl,
            OidcClients.WebPortal => _portalBaseUrl,
            _ => throw new ArgumentException(
                $"El cliente OIDC '{clientId}' no tiene una URL base configurada.", nameof(clientId))
        };

        return QueryHelpers.AddQueryString($"{baseUrl}{PasswordResetPath}", "token", plainToken);
    }

    /// <summary>
    /// Normaliza la URL base para que termine en exactamente una barra. Es lo que permite concatenar
    /// la ruta sin barra inicial y que funcionen por igual una URL http y un esquema de deep link:
    /// recortar las barras finales convertiría <c>cauce://</c> en <c>cauce:</c> y produciría un
    /// enlace malformado.
    /// </summary>
    /// <param name="baseUrl">URL base tal como viene de la configuración.</param>
    /// <returns>La URL base terminada en una sola barra.</returns>
    private static string NormalizeBaseUrl(string baseUrl)
    {
        return baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
    }
}
