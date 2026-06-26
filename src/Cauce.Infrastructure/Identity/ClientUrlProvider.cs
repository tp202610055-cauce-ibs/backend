using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Infrastructure.Email;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Implementación de <see cref="IClientUrlProvider"/> que construye enlaces de
/// cara al cliente a partir de la URL base configurada en <see cref="EmailOptions"/>.
/// </summary>
public sealed class ClientUrlProvider : IClientUrlProvider
{
    private readonly string _appBaseUrl;

    /// <summary>
    /// Inicializa el proveedor con la URL base de la aplicación.
    /// </summary>
    /// <param name="emailOptions">Opciones de correo que contienen la URL base.</param>
    public ClientUrlProvider(IOptions<EmailOptions> emailOptions)
    {
        _appBaseUrl = emailOptions.Value.AppBaseUrl.TrimEnd('/');
    }

    /// <inheritdoc />
    public string BuildPasswordResetLink(string plainToken)
    {
        var baseLink = $"{_appBaseUrl}/auth/password-reset";
        return QueryHelpers.AddQueryString(baseLink, "token", plainToken);
    }
}
