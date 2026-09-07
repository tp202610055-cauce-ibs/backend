using System.Net;
using System.Net.Http.Json;
using System.Text;
using Cauce.Api.IntegrationTests.Identity.Support;
using FluentAssertions;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas de <c>POST /api/v1/auth/verification-email/resend</c> (acta A40). Cubren la validación de
/// entrada y, sobre todo, que el rate limit particione por correo normalizado y no por IP, que es lo
/// que justifica el middleware del acta A44. Requieren Docker (PostgreSQL + Redis).
/// </summary>
/// <remarks>
/// Cada prueba usa un correo propio: el limitador mantiene sus particiones en memoria durante toda la
/// vida de la aplicación de prueba, así que compartir correo entre pruebas las acoplaría.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class VerificationEmailResendApiTests
    : Prompt5IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string Endpoint = "/api/v1/auth/verification-email/resend";

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis/KeyDB.</param>
    public VerificationEmailResendApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task Resend_UnknownEmail_Returns200()
    {
        SkipIfUnavailable();

        // No revela que la cuenta no existe.
        var response = await PostAsync("desconocido-1@cauce.local");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact]
    public async Task Resend_UnverifiedUser_RequestsTheEmailFromKeycloak()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();

        var response = await PostAsync(patient.Email);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Factory.KeycloakClient.VerifyEmailsSent.Should().Contain(patient.KeycloakId);
    }

    [SkippableTheory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sin-arroba")]
    public async Task Resend_InvalidEmail_Returns400(string email)
    {
        SkipIfUnavailable();

        var response = await PostAsync(email);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task Resend_MalformedJson_Returns400()
    {
        SkipIfUnavailable();
        var client = Factory.CreateClient();

        var response = await client.PostAsync(
            Endpoint,
            new StringContent("{ esto no es json", Encoding.UTF8, "application/json"));

        // El middleware de partición tolera el cuerpo inválido y cae a la IP; el binder responde 400.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task Resend_FourthRequestForTheSameEmail_Returns429()
    {
        SkipIfUnavailable();
        const string Email = "limite-1@cauce.local";

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            (await PostAsync(Email)).StatusCode.Should().Be(HttpStatusCode.OK, $"la petición {attempt} entra en el cupo");
        }

        var throttled = await PostAsync(Email);

        throttled.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [SkippableFact]
    public async Task Resend_SameEmailInDifferentCase_SharesTheRateLimitBucket()
    {
        SkipIfUnavailable();
        const string Lower = "limite-2@cauce.local";
        const string Upper = "LIMITE-2@CAUCE.LOCAL";

        await PostAsync(Lower);
        await PostAsync(Upper);
        await PostAsync(Lower);

        // Si la partición no normalizara la caja, este cuarto intento abriría una cubeta nueva y pasaría.
        var throttled = await PostAsync(Upper);

        throttled.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [SkippableFact]
    public async Task Resend_DifferentEmailsFromTheSameIp_AreNotThrottled()
    {
        SkipIfUnavailable();

        // Cuatro correos distintos desde el mismo cliente. Con partición por IP, el cuarto sería 429.
        for (var index = 1; index <= 4; index++)
        {
            var response = await PostAsync($"vecino-{index}@cauce.local");
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"el correo {index} tiene su propia cubeta");
        }
    }

    private async Task<HttpResponseMessage> PostAsync(string email)
    {
        var client = Factory.CreateClient();
        return await client.PostAsJsonAsync(Endpoint, new { email });
    }
}
