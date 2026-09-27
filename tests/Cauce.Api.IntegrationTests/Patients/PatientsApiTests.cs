using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Persistence.Seeders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.Patients;

/// <summary>
/// Pruebas de integración end-to-end del módulo de pacientes. Reutilizan la
/// infraestructura de pruebas del módulo de identidad y se omiten si Docker no
/// está disponible.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PatientsApiTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private CustomWebApplicationFactory _factory = null!;

    /// <summary>
    /// Inicializa la prueba con el fixture de PostgreSQL.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    public PatientsApiTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        if (!_postgres.IsAvailable)
        {
            return;
        }

        _factory = new CustomWebApplicationFactory(_postgres.ConnectionString);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<UserRolesSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<AllergiesSeeder>().SeedAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    // ----- Creación de perfil -----

    [SkippableFact]
    public async Task CreateProfile_AsPatient_Returns201AndPersistsProfile()
    {
        SkipIfNoDocker();
        var (userId, keycloakId) = await SeedPatientAsync(UniqueEmail());
        var client = PatientClient(keycloakId);

        var response = await client.PostAsJsonAsync("/api/v1/patients/profile", ValidProfileBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        (await db.PatientProfiles.AsNoTracking().AnyAsync(p => p.UserId == userId && !p.OnboardingCompleted)).Should().BeTrue();
    }

    [SkippableFact]
    public async Task CreateProfile_WithInvalidBmi_Returns400()
    {
        SkipIfNoDocker();
        var (_, keycloakId) = await SeedPatientAsync(UniqueEmail());
        var client = PatientClient(keycloakId);

        var response = await client.PostAsJsonAsync("/api/v1/patients/profile", ValidProfileBody(weightKg: 600m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task CreateProfile_WithAgeBelow18_Returns400()
    {
        SkipIfNoDocker();
        var (_, keycloakId) = await SeedPatientAsync(UniqueEmail());
        var client = PatientClient(keycloakId);
        var minor = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-15);

        var response = await client.PostAsJsonAsync("/api/v1/patients/profile", ValidProfileBody(dateOfBirth: minor));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task CreateProfile_WhenProfileExists_Returns409()
    {
        SkipIfNoDocker();
        var (_, keycloakId) = await SeedPatientAsync(UniqueEmail());
        var client = PatientClient(keycloakId);

        (await client.PostAsJsonAsync("/api/v1/patients/profile", ValidProfileBody())).StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await client.PostAsJsonAsync("/api/v1/patients/profile", ValidProfileBody());

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [SkippableFact]
    public async Task CreateProfile_WhenPatientUsedInvitationCode_AlsoCreatesNutritionistAssignment()
    {
        SkipIfNoDocker();
        var (nutritionistId, _) = await SeedNutritionistAsync(UniqueEmail());
        var (patientId, keycloakId) = await SeedPatientAsync(UniqueEmail());
        await SeedConsumedInvitationAsync(nutritionistId, patientId);
        var client = PatientClient(keycloakId);

        var response = await client.PostAsJsonAsync("/api/v1/patients/profile", ValidProfileBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("nutritionistAssigned").GetBoolean().Should().BeTrue();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        (await db.NutritionistPatients.AsNoTracking()
            .AnyAsync(np => np.NutritionistId == nutritionistId && np.PatientId == patientId)).Should().BeTrue();
    }

    [SkippableFact]
    public async Task CreateProfile_WhenPatientDidNotUseInvitationCode_DoesNotCreateAssignment()
    {
        SkipIfNoDocker();
        var (patientId, keycloakId) = await SeedPatientAsync(UniqueEmail());
        var client = PatientClient(keycloakId);

        var response = await client.PostAsJsonAsync("/api/v1/patients/profile", ValidProfileBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("nutritionistAssigned").GetBoolean().Should().BeFalse();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        (await db.NutritionistPatients.AsNoTracking().AnyAsync(np => np.PatientId == patientId)).Should().BeFalse();
    }

    [SkippableFact]
    public async Task CreateProfile_AsNutritionist_Returns403()
    {
        SkipIfNoDocker();
        var (_, keycloakId) = await SeedNutritionistAsync(UniqueEmail());
        var client = NutritionistClient(keycloakId);

        var response = await client.PostAsJsonAsync("/api/v1/patients/profile", ValidProfileBody());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ----- Actualización y consulta de perfil -----

    [SkippableFact]
    public async Task UpdateProfile_AsOwner_Returns200AndOnlyUpdatesProvidedFields()
    {
        SkipIfNoDocker();
        var (_, keycloakId) = await SeedPatientAsync(UniqueEmail());
        var client = PatientClient(keycloakId);
        await client.PostAsJsonAsync("/api/v1/patients/profile", ValidProfileBody());

        var update = await client.PutAsJsonAsync("/api/v1/patients/profile", new { weightKg = 80m });

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await (await client.GetAsync("/api/v1/patients/profile")).Content.ReadFromJsonAsync<JsonElement>();
        profile.GetProperty("weightKg").GetDecimal().Should().Be(80m);
        profile.GetProperty("heightCm").GetDecimal().Should().Be(175m);
        profile.GetProperty("ibsSubtype").GetString().Should().Be("IbsM");
    }

    [SkippableFact]
    public async Task GetProfile_WithCompleteData_ReturnsBmiAgeAllergiesAndAssignedNutritionist()
    {
        SkipIfNoDocker();
        var (nutritionistId, _) = await SeedNutritionistAsync(UniqueEmail());
        var (patientId, keycloakId) = await SeedPatientAsync(UniqueEmail());
        await SeedConsumedInvitationAsync(nutritionistId, patientId);
        var client = PatientClient(keycloakId);
        await client.PostAsJsonAsync("/api/v1/patients/profile", ValidProfileBody());
        var allergyId = await FirstActiveAllergyIdAsync();
        await client.PostAsJsonAsync("/api/v1/patients/allergies", new { allergyId, severity = "Moderate", notes = (string?)null });

        var profile = await (await client.GetAsync("/api/v1/patients/profile")).Content.ReadFromJsonAsync<JsonElement>();

        profile.GetProperty("bmi").GetDecimal().Should().BeGreaterThan(0);
        profile.GetProperty("age").GetInt32().Should().BeGreaterThan(0);
        profile.GetProperty("allergies").GetArrayLength().Should().Be(1);
        profile.GetProperty("assignedNutritionist").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    // ----- Alergias del paciente -----

    [SkippableFact]
    public async Task ListMyAllergies_AsPatient_ReturnsEmpty_WhenNothingDeclared()
    {
        SkipIfNoDocker();
        var (_, keycloakId) = await SeedPatientAsync(UniqueEmail());
        var client = PatientClient(keycloakId);

        var response = await client.GetAsync("/api/v1/patients/allergies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var allergies = await response.Content.ReadFromJsonAsync<JsonElement>();
        allergies.GetArrayLength().Should().Be(0);
    }

    [SkippableFact]
    public async Task DeclareAllergy_WithValidAllergyId_Returns201()
    {
        SkipIfNoDocker();
        var (_, keycloakId) = await SeedPatientAsync(UniqueEmail());
        var client = PatientClient(keycloakId);
        var allergyId = await FirstActiveAllergyIdAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/patients/allergies", new { allergyId, severity = "Mild", notes = "leve" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [SkippableFact]
    public async Task DeclareAllergy_WithInactiveAllergy_Returns404()
    {
        SkipIfNoDocker();
        var (_, keycloakId) = await SeedPatientAsync(UniqueEmail());
        var client = PatientClient(keycloakId);
        var inactiveId = await SeedInactiveAllergyAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/patients/allergies", new { allergyId = inactiveId, severity = "Mild", notes = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SkippableFact]
    public async Task DeclareAllergy_WithDuplicate_Returns409()
    {
        SkipIfNoDocker();
        var (_, keycloakId) = await SeedPatientAsync(UniqueEmail());
        var client = PatientClient(keycloakId);
        var allergyId = await FirstActiveAllergyIdAsync();
        await client.PostAsJsonAsync("/api/v1/patients/allergies", new { allergyId, severity = "Mild", notes = (string?)null });

        var duplicate = await client.PostAsJsonAsync("/api/v1/patients/allergies", new { allergyId, severity = "Severe", notes = (string?)null });

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [SkippableFact]
    public async Task RemoveAllergy_AsOwner_Returns204()
    {
        SkipIfNoDocker();
        var (_, keycloakId) = await SeedPatientAsync(UniqueEmail());
        var client = PatientClient(keycloakId);
        var allergyId = await FirstActiveAllergyIdAsync();
        var declared = await client.PostAsJsonAsync("/api/v1/patients/allergies", new { allergyId, severity = "Mild", notes = (string?)null });
        var declaredJson = await declared.Content.ReadFromJsonAsync<JsonElement>();
        var patientAllergyId = declaredJson.GetProperty("patientAllergyId").GetGuid();

        var response = await client.DeleteAsync($"/api/v1/patients/allergies/{patientAllergyId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ----- Catálogo de alergias -----

    [SkippableFact]
    public async Task ListAllergiesCatalog_AsAnyAuthenticated_Returns200()
    {
        SkipIfNoDocker();
        var client = PatientClient(Guid.NewGuid().ToString());

        var response = await client.GetAsync("/api/v1/allergies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var catalog = await response.Content.ReadFromJsonAsync<JsonElement>();
        catalog.GetArrayLength().Should().Be(10);
    }

    // ----- Endpoints del nutricionista -----

    [SkippableFact]
    public async Task ListAssignedPatients_AsNutritionist_ReturnsActiveAssignments()
    {
        SkipIfNoDocker();
        var (nutritionistId, nutritionistKeycloakId) = await SeedNutritionistAsync(UniqueEmail());
        var (patientId, patientKeycloakId) = await SeedPatientAsync(UniqueEmail());
        await SeedConsumedInvitationAsync(nutritionistId, patientId);
        await PatientClient(patientKeycloakId).PostAsJsonAsync("/api/v1/patients/profile", ValidProfileBody());

        var response = await NutritionistClient(nutritionistKeycloakId).GetAsync("/api/v1/nutritionists/me/patients");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var patients = await response.Content.ReadFromJsonAsync<JsonElement>();
        patients.GetArrayLength().Should().Be(1);
    }

    [SkippableFact]
    public async Task GetAssignedPatientDetail_WithoutAssignment_Returns403()
    {
        SkipIfNoDocker();
        var (_, nutritionistKeycloakId) = await SeedNutritionistAsync(UniqueEmail());
        var client = NutritionistClient(nutritionistKeycloakId);

        var response = await client.GetAsync($"/api/v1/nutritionists/me/patients/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [SkippableFact]
    public async Task GetAssignedPatientDetail_WithoutProfile_Returns200WithClinicalFieldsInNull()
    {
        SkipIfNoDocker();
        var (nutritionistId, nutritionistKeycloakId) = await SeedNutritionistAsync(UniqueEmail());
        var (patientId, _) = await SeedPatientAsync(UniqueEmail());
        using (var scope = _factory.Services.CreateScope())
        {
            // Vínculo por canje post-registro (acta A41): el paciente aparece en el panel antes del perfil.
            var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
            db.NutritionistPatients.Add(NutritionistPatient.Establish(Guid.NewGuid(), nutritionistId, patientId, null, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        var response = await NutritionistClient(nutritionistKeycloakId).GetAsync($"/api/v1/nutritionists/me/patients/{patientId}");

        // Acta A69: antes respondía 404 patient_profile_not_found.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("patientUserId").GetGuid().Should().Be(patientId);
        detail.GetProperty("fullName").GetString().Should().Be("Paciente Seed");
        detail.GetProperty("onboardingCompleted").GetBoolean().Should().BeFalse();
        foreach (var field in new[] { "age", "bmi", "bmiCategory", "ibsSubtype", "biologicalSex", "diagnosisDate", "medications" })
        {
            detail.GetProperty(field).ValueKind.Should().Be(JsonValueKind.Null, $"{field} no tiene perfil del que salir");
        }

        detail.GetProperty("allergies").GetArrayLength().Should().Be(0);
    }

    [SkippableFact]
    public async Task GetAssignedPatientDetail_WithProfile_IncludesSexDiagnosisAndMedicationsButNotTheCode()
    {
        SkipIfNoDocker();
        var (nutritionistId, nutritionistKeycloakId) = await SeedNutritionistAsync(UniqueEmail());
        var (patientId, patientKeycloakId) = await SeedPatientAsync(UniqueEmail());
        await SeedConsumedInvitationAsync(nutritionistId, patientId);
        var diagnosisDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-2);
        var profile = await PatientClient(patientKeycloakId).PostAsJsonAsync("/api/v1/patients/profile", new
        {
            dateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30),
            biologicalSex = "Female",
            weightKg = 62.5m,
            heightCm = 162m,
            ibsSubtype = "IbsD",
            diagnosisDate,
            medications = "Mebeverina 135 mg"
        });
        profile.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await NutritionistClient(nutritionistKeycloakId).GetAsync($"/api/v1/nutritionists/me/patients/{patientId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("ibsSubtype").GetString().Should().Be("IbsD");
        detail.GetProperty("biologicalSex").GetString().Should().Be("Female");
        detail.GetProperty("diagnosisDate").GetString().Should().Be(diagnosisDate.ToString("yyyy-MM-dd"));
        detail.GetProperty("medications").GetString().Should().Be("Mebeverina 135 mg");
        detail.GetProperty("age").GetInt32().Should().BeGreaterThan(0);
        // Acta A59: el código de paciente no va a las vistas de atención, donde quedaría junto al nombre.
        detail.TryGetProperty("patientCode", out _).Should().BeFalse();
    }

    // ----- Helpers -----

    private void SkipIfNoDocker()
    {
        Skip.IfNot(_postgres.IsAvailable, "Docker no está disponible; se omiten las pruebas de integración.");
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@cauce.local";

    private static object ValidProfileBody(decimal weightKg = 70m, decimal heightCm = 175m, DateOnly? dateOfBirth = null) => new
    {
        dateOfBirth = dateOfBirth ?? DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30),
        biologicalSex = "Male",
        weightKg,
        heightCm,
        ibsSubtype = "IbsM",
        diagnosisDate = (DateOnly?)null,
        medications = (string?)null
    };

    private HttpClient PatientClient(string keycloakId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtBuilder.Build(keycloakId, "patient@cauce.local", UserRoles.Patient));
        return client;
    }

    private HttpClient NutritionistClient(string keycloakId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtBuilder.Build(keycloakId, "nutri@cauce.local", UserRoles.Nutritionist));
        return client;
    }

    private async Task<(Guid Id, string KeycloakId)> SeedPatientAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var roleId = await db.UserRoles.Where(r => r.RoleName == UserRoles.Patient).Select(r => r.RoleId).FirstAsync();
        var keycloakId = Guid.NewGuid().ToString();
        var user = User.CreatePatient(Guid.NewGuid(), keycloakId, email, "Paciente Seed", roleId, TestPatientCodes.Next());
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, keycloakId);
    }

    private async Task<(Guid Id, string KeycloakId)> SeedNutritionistAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var roleId = await db.UserRoles.Where(r => r.RoleName == UserRoles.Nutritionist).Select(r => r.RoleId).FirstAsync();
        var keycloakId = Guid.NewGuid().ToString();
        var user = User.CreateNutritionist(Guid.NewGuid(), keycloakId, email, "Nutri Seed", roleId);
        // La fábrica lo crea pendiente (acta A51); estas pruebas necesitan uno operativo.
        user.Activate();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, keycloakId);
    }

    private async Task SeedConsumedInvitationAsync(Guid nutritionistId, Guid patientId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var code = "INV" + new string(Guid.NewGuid().ToString("N").ToUpperInvariant().Where(char.IsLetterOrDigit).Take(7).ToArray());
        var invitation = InvitationCode.Generate(Guid.NewGuid(), code, nutritionistId, DateTime.UtcNow, InvitationCode.Validity);
        invitation.MarkAsUsed(patientId, DateTime.UtcNow.AddMinutes(1));
        db.InvitationCodes.Add(invitation);
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedInactiveAllergyAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var allergy = Allergy.SeedEntry(Guid.NewGuid(), $"Inactiva-{Guid.NewGuid():N}", AllergyType.Sensitivity, "inactiva");
        allergy.Deactivate();
        db.Allergies.Add(allergy);
        await db.SaveChangesAsync();
        return allergy.Id;
    }

    private async Task<Guid> FirstActiveAllergyIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        return await db.Allergies.AsNoTracking().Where(a => a.IsActive).Select(a => a.Id).FirstAsync();
    }
}
