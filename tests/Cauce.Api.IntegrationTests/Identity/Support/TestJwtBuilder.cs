using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Cauce.Api.IntegrationTests.Identity.Support;

/// <summary>
/// Constructor de tokens JWT de prueba firmados con una clave simétrica conocida.
/// La fábrica de pruebas configura el middleware JWT para validar contra esta misma
/// clave, emisor y audiencia.
/// </summary>
public static class TestJwtBuilder
{
    /// <summary>
    /// Emisor esperado en los tokens de prueba.
    /// </summary>
    public const string Issuer = "https://test.cauce.local/realms/cauce";

    /// <summary>
    /// Audiencia esperada en los tokens de prueba.
    /// </summary>
    public const string Audience = "cauce-backend";

    private const string SigningSecret = "cauce-integration-tests-signing-secret-0123456789";

    /// <summary>
    /// Clave de firma simétrica compartida entre el constructor y la validación.
    /// </summary>
    public static SymmetricSecurityKey SecurityKey { get; } =
        new(Encoding.UTF8.GetBytes(SigningSecret));

    /// <summary>
    /// Construye un token JWT firmado con los claims indicados.
    /// </summary>
    /// <param name="subject">Identificador de Keycloak (claim <c>sub</c>).</param>
    /// <param name="email">Correo electrónico.</param>
    /// <param name="roles">Roles del realm a incluir en <c>realm_access</c>.</param>
    /// <returns>El token JWT serializado.</returns>
    public static string Build(string subject, string email, params string[] roles)
    {
        var realmAccess = JsonSerializer.Serialize(new { roles });

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
            new(JwtRegisteredClaimNames.Email, email),
            new("preferred_username", email),
            new("realm_access", realmAccess, JsonClaimValueTypes.Json)
        };

        var credentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
