using System.Net;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using FluentAssertions;

namespace Cauce.Api.IntegrationTests.ClinicalRegistry;

/// <summary>
/// Pruebas de integración de los parámetros obligatorios de los GET por rango de fechas y de las búsquedas.
/// Distinguen el 400 automático de <c>[ApiController]</c>, sin <c>errorCode</c>, del 400 <c>validation_error</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RangeAndSearchParametersApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string ValidFrom = "2026-10-01T00:00:00Z";
    private const string ValidTo = "2026-10-02T00:00:00Z";

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public RangeAndSearchParametersApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private async Task<HttpClient> PatientClientAsync()
    {
        var patient = await SeedPatientAsync();
        return PatientClient(patient.KeycloakId, patient.Email);
    }

    private static string Url(string path, params (string Name, string Value)[] parameters) =>
        $"{path}?{string.Join("&", parameters.Select(p => $"{p.Name}={Uri.EscapeDataString(p.Value)}"))}";

    private static async Task<JsonElement> ReadBadRequestAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, raw);
        return JsonDocument.Parse(raw).RootElement.Clone();
    }

    private static async Task AssertBindingErrorAsync(HttpResponseMessage response, string key)
    {
        var body = await ReadBadRequestAsync(response);
        body.GetProperty("errors").TryGetProperty(key, out _).Should().BeTrue(body.GetRawText());
        body.TryGetProperty("errorCode", out _).Should().BeFalse(body.GetRawText());
    }

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response, string key)
    {
        var body = await ReadBadRequestAsync(response);
        body.TryGetProperty("errorCode", out var errorCode).Should().BeTrue(body.GetRawText());
        errorCode.GetString().Should().Be("validation_error");
        body.GetProperty("errors").TryGetProperty(key, out _).Should().BeTrue(body.GetRawText());
    }

    [SkippableTheory]
    [InlineData("/api/v1/meals")]
    [InlineData("/api/v1/symptoms")]
    [InlineData("/api/v1/clinical-notes")]
    [InlineData("/api/v1/history")]
    public async Task GetRange_WithoutFrom_Returns400BindingError(string path)
    {
        SkipIfUnavailable();
        var client = await PatientClientAsync();

        var response = await client.GetAsync(Url(path, ("to", ValidTo)));

        await AssertBindingErrorAsync(response, "from");
    }

    [SkippableTheory]
    [InlineData("/api/v1/meals")]
    [InlineData("/api/v1/symptoms")]
    [InlineData("/api/v1/clinical-notes")]
    [InlineData("/api/v1/history")]
    public async Task GetRange_WithoutTo_Returns400BindingError(string path)
    {
        SkipIfUnavailable();
        var client = await PatientClientAsync();

        var response = await client.GetAsync(Url(path, ("from", ValidFrom)));

        await AssertBindingErrorAsync(response, "to");
    }

    [SkippableTheory]
    [InlineData("/api/v1/meals")]
    [InlineData("/api/v1/symptoms")]
    [InlineData("/api/v1/clinical-notes")]
    [InlineData("/api/v1/history")]
    public async Task GetRange_ToBeforeFrom_Returns400ValidationError(string path)
    {
        SkipIfUnavailable();
        var client = await PatientClientAsync();

        var response = await client.GetAsync(Url(path, ("from", ValidTo), ("to", ValidFrom)));

        await AssertValidationErrorAsync(response, "to");
    }

    [SkippableTheory]
    [InlineData("/api/v1/meals", "2026-10-01T00:00:00Z", "2026-10-02T00:00:00Z")]
    [InlineData("/api/v1/meals", "2026-10-01T00:00:00-05:00", "2026-10-02T00:00:00-05:00")]
    [InlineData("/api/v1/symptoms", "2026-10-01T00:00:00Z", "2026-10-02T00:00:00Z")]
    [InlineData("/api/v1/symptoms", "2026-10-01T00:00:00-05:00", "2026-10-02T00:00:00-05:00")]
    [InlineData("/api/v1/clinical-notes", "2026-10-01T00:00:00Z", "2026-10-02T00:00:00Z")]
    [InlineData("/api/v1/clinical-notes", "2026-10-01T00:00:00-05:00", "2026-10-02T00:00:00-05:00")]
    [InlineData("/api/v1/history", "2026-10-01T00:00:00Z", "2026-10-02T00:00:00Z")]
    [InlineData("/api/v1/history", "2026-10-01T00:00:00-05:00", "2026-10-02T00:00:00-05:00")]
    public async Task GetRange_ValidRange_Returns200(string path, string from, string to)
    {
        SkipIfUnavailable();
        var client = await PatientClientAsync();

        var response = await client.GetAsync(Url(path, ("from", from), ("to", to)));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [SkippableTheory]
    [InlineData("/api/v1/meals", "2026-10-01T00:00:00")]
    [InlineData("/api/v1/meals", "2026-10-01")]
    [InlineData("/api/v1/symptoms", "2026-10-01T00:00:00")]
    [InlineData("/api/v1/symptoms", "2026-10-01")]
    [InlineData("/api/v1/clinical-notes", "2026-10-01T00:00:00")]
    [InlineData("/api/v1/clinical-notes", "2026-10-01")]
    [InlineData("/api/v1/history", "2026-10-01T00:00:00")]
    [InlineData("/api/v1/history", "2026-10-01")]
    public async Task GetRange_FromWithoutTimeZone_Returns400ValidationError(string path, string from)
    {
        SkipIfUnavailable();
        var client = await PatientClientAsync();

        var response = await client.GetAsync(Url(path, ("from", from), ("to", ValidTo)));

        await AssertValidationErrorAsync(response, "from");
    }

    [SkippableTheory]
    [InlineData("/api/v1/foods/search", false)]
    [InlineData("/api/v1/glossary/search", false)]
    [InlineData("/api/v1/foods/search", true)]
    [InlineData("/api/v1/glossary/search", true)]
    public async Task Search_MissingOrEmptyQuery_Returns400BindingError(string path, bool sendEmptyQuery)
    {
        SkipIfUnavailable();
        var client = await PatientClientAsync();

        var response = await client.GetAsync(sendEmptyQuery ? Url(path, ("q", string.Empty)) : path);

        await AssertBindingErrorAsync(response, "q");
    }
}
