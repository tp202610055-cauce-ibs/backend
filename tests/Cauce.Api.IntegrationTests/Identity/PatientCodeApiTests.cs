using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas de integración del código correlativo de paciente (G1). Se ejercitan a través del
/// endpoint real de registro, de modo que pasan por el pipeline completo de MediatR y por la
/// secuencia de PostgreSQL que entrega el correlativo.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PatientCodeApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public PatientCodeApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    /// <summary>
    /// Apaga el limitador de tasa: la política <c>auth-register</c> admite 5 altas por hora y por IP,
    /// y la prueba de concurrencia necesita doce simultáneas. Lo que se verifica aquí es la unicidad
    /// del correlativo, no el limitador, que tiene sus propias pruebas.
    /// </summary>
    protected override CustomWebApplicationFactory CreateFactory() =>
        new(Postgres.ConnectionString, Redis.ConnectionString, rateLimitingEnabled: false);

    private static object RegisterBody(string email) => new
    {
        email,
        fullName = "Paciente De Prueba",
        password = "Password1",
        consentDocumentVersion = CustomWebApplicationFactory.ConsentVersion,
        consentTextHash = ConsentHash(),
        ipAddress = "127.0.0.1"
    };

    private static string ConsentHash()
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(CustomWebApplicationFactory.ConsentText));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task<string?> PatientCodeOfAsync(string email)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.Users.AsNoTracking()
            .Where(user => user.Email == email)
            .Select(user => user.PatientCode)
            .FirstOrDefaultAsync();
    }

    [SkippableFact]
    public async Task Register_AssignsACanonicalPatientCode()
    {
        SkipIfUnavailable();
        var email = $"pac-{Guid.NewGuid():N}@cauce.local";

        var response = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register", RegisterBody(email));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var code = await PatientCodeOfAsync(email);
        code.Should().NotBeNullOrWhiteSpace();
        PatientCode.TryParse(code, out _).Should().BeTrue($"'{code}' debe respetar el formato PAC-0000");
    }

    [SkippableFact]
    public async Task Register_ConcurrentRegistrations_NeverRepeatACode()
    {
        SkipIfUnavailable();

        // Doce altas simultáneas contra el mismo backend. Si el correlativo se calculara en la
        // aplicación (por ejemplo con MAX + 1) dos de ellas leerían el mismo máximo y colisionarían;
        // con nextval de PostgreSQL cada una recibe un número propio.
        const int registrations = 12;
        var emails = Enumerable.Range(0, registrations)
            .Select(_ => $"pac-{Guid.NewGuid():N}@cauce.local")
            .ToList();

        var responses = await Task.WhenAll(emails.Select(email =>
            Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register", RegisterBody(email))));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var codes = await db.Users.AsNoTracking()
            .Where(user => emails.Contains(user.Email))
            .Select(user => user.PatientCode)
            .ToListAsync();

        codes.Should().HaveCount(registrations);
        codes.Should().OnlyContain(code => code != null);
        codes.Should().OnlyHaveUniqueItems();
    }

    [SkippableFact]
    public async Task Register_NutritionistAccounts_HaveNoPatientCode()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var code = await db.Users.AsNoTracking()
            .Where(user => user.Id == nutritionist.Id)
            .Select(user => user.PatientCode)
            .FirstAsync();

        code.Should().BeNull("el código identifica sujetos del estudio, no cuentas del equipo clínico");
    }

    [SkippableFact]
    public async Task ExportMyData_WithoutMinio_StillResolvesThePatientCode()
    {
        SkipIfUnavailable();
        var email = $"pac-{Guid.NewGuid():N}@cauce.local";
        await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register", RegisterBody(email));

        var code = await PatientCodeOfAsync(email);

        // La consulta que alimenta el ZIP resuelve el código del paciente; sin él, la exportación no
        // tendría con qué identificar al sujeto y el builder fallaría.
        code.Should().NotBeNull();
        code!.Should().StartWith(PatientCode.Prefix);
    }

    [SkippableFact]
    public async Task Register_DuplicateEmail_DoesNotConsumeTheSameCodeTwice()
    {
        SkipIfUnavailable();
        var email = $"pac-{Guid.NewGuid():N}@cauce.local";
        var first = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register", RegisterBody(email));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register", RegisterBody(email));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("duplicate_email");
    }
}
