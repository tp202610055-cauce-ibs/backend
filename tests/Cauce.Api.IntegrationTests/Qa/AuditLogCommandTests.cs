using Cauce.Api.Commands;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Api.IntegrationTests.Qa.Support;
using Cauce.Application.Common.Auditing;
using Cauce.Application.Common.Identity;
using Cauce.Domain.Auditing;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Recommendations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Qa;

/// <summary>
/// Pruebas de <c>audit-log</c>: qué filas salen para un caso, cómo se nombra al autor, que no se imprimen IP, user
/// agent ni hashes, que no escribe nada y sus guardas. Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AuditLogCommandTests
    : QaCommandTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string Patient = "paciente.auditoria@ejemplo.com";
    private const string OtherPatient = "paciente.otro@ejemplo.com";
    private const string SharedNutritionist = "nutricionista.compartida@ejemplo.com";
    private const string UnknownEmail = "noexiste.auditoria@ejemplo.com";
    private const string IpAddress = "203.0.113.7";
    private const string UserAgent = "QaReviewerAgent/1.0";

    public AuditLogCommandTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private static string Manifest() => new QaManifestBuilder()
        .Patient(Patient)
        .Patient(OtherPatient)
        .Nutritionist(SharedNutritionist)
        .MustNotExist(UnknownEmail)
        .Case("CP901", [Patient, SharedNutritionist], mustNotExist: [UnknownEmail])
        .Case("CP902", [OtherPatient, SharedNutritionist])
        .Build();

    [SkippableFact]
    public async Task AuditLog_Case_ShowsItsRowsWithAuthorsAndHidesIpUserAgentAndHashes()
    {
        SkipIfUnavailable();
        var manifest = await SeedAsync();
        var hash = AuditHash.Sha256Hex("valor de prueba");
        var (patientId, otherPatientId, nutritionistId) = (await UserIdAsync(Patient), await UserIdAsync(OtherPatient), await UserIdAsync(SharedNutritionist));
        var ownRecommendation = await AddManualRecommendationAsync(patientId, nutritionistId);
        var otherRecommendation = await AddManualRecommendationAsync(otherPatientId, nutritionistId);
        await AddAuditAsync(
            Row(patientId, AuditActionType.FailedLogin, "User", null, AuthAuditContext.Build(LoginChannel.Mobile, AuthFailureCauses.WrongPassword), hash),
            Row(nutritionistId, AuditActionType.Login, "User", null, AuthAuditContext.Build(LoginChannel.Portal, null), null),
            Row(null, AuditActionType.FailedLogin, "User", null, AuthAuditContext.Build(LoginChannel.Mobile, AuthFailureCauses.WrongPassword), null),
            Row(null, AuditActionType.PasswordResetRequest, "User", null, $"{{\"email\":\"{AuditMask.Email(Patient)}\"}}", hash),
            Row(nutritionistId, AuditActionType.Approve, "Recommendation", ownRecommendation, null, hash),
            Row(nutritionistId, AuditActionType.Approve, "Recommendation", otherRecommendation, null, hash));
        var before = await CountsAsync();

        var code = await RunCapturingAsync("audit-log", "CP901", "--manifest", manifest);

        code.Should().Be(CommandRunner.Success, CauceLog);
        var lines = CommandLines();
        var ownTag = "#" + ownRecommendation.ToString("N")[..8];
        var otherTag = "#" + otherRecommendation.ToString("N")[..8];
        lines.Should().Contain(line => line.Contains("| Ingreso fallido | Cuenta | causa=wrong_password, canal=mobile | " + Patient));
        lines.Should().Contain(line => line.Contains("| Ingreso exitoso | Cuenta | canal=portal | " + SharedNutritionist));
        lines.Should().Contain(line => line.Contains("| Ingreso fallido |") && line.EndsWith("| correo sin cuenta (no se puede atribuir)", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("| Solicitud de recuperación de contraseña |") && line.Contains("correo=p***@ejemplo.com"));
        lines.Should().Contain(line => line.Contains($"| Aprobación | Recomendación {ownTag} |") && line.EndsWith(SharedNutritionist, StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains($"| Creación | Recomendación {ownTag} | origen=trigger | Sistema (proceso automático)"));
        lines.Should().NotContain(line => line.Contains(otherTag));
        CapturedLog.Should().NotContain(line => line.Contains(IpAddress) || line.Contains(UserAgent) || line.Contains(hash)
            || line.Contains(PatientPassword) || line.Contains(NutritionistPassword));
        (await CountsAsync()).Should().BeEquivalentTo(before);
    }

    [SkippableFact]
    public async Task AuditLog_ActionFilterAndLimit_KeepsTheMostRecentMatchingRows()
    {
        SkipIfUnavailable();
        var manifest = await SeedAsync();
        var patientId = await UserIdAsync(Patient);
        var now = DateTime.UtcNow;
        await AddAuditAsync(
            Row(patientId, AuditActionType.FailedLogin, "User", null, null, null, now.AddMinutes(-3)),
            Row(patientId, AuditActionType.FailedLogin, "User", null, null, null, now.AddMinutes(-2)),
            Row(patientId, AuditActionType.Login, "User", null, null, null, now.AddMinutes(-1)),
            Row(patientId, AuditActionType.FailedLogin, "User", null, null, null, now.AddDays(-2)));

        var code = await RunCapturingAsync("audit-log", Patient, "--action", "failed_login", "--limit", "1", "--manifest", manifest);

        code.Should().Be(CommandRunner.Success, CauceLog);
        var rows = CommandLines().Where(line => line.Contains(" Lima | ", StringComparison.Ordinal)).ToList();
        rows.Should().ContainSingle().Which.Should().Contain("| Ingreso fallido |");
        CapturedLog.Should().Contain(line => line.Contains("audit-log " + Patient + ": 1 fila(s)"));
    }

    [SkippableFact]
    public async Task AuditLog_PatientAnonymizedByDeletion_IsFoundThroughKeycloak()
    {
        SkipIfUnavailable();
        var manifest = await SeedAsync();
        var patientId = await UserIdAsync(Patient);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var user = await db.Users.SingleAsync(candidate => candidate.Id == patientId);
            user.Anonymize(user.RoleId);
            await db.SaveChangesAsync();
        }

        await AddAuditAsync(Row(patientId, AuditActionType.Delete, "User", patientId, "{\"operation\":\"anonymization\",\"keycloak_action\":\"disable\"}", null));

        var code = await RunCapturingAsync("audit-log", Patient, "--manifest", manifest);

        code.Should().Be(CommandRunner.Success, CauceLog);
        CommandLines().Should().Contain(line => line.Contains("| Eliminación o anonimización | Cuenta #") && line.Contains("operación=anonymization, (otros datos omitidos) | " + Patient));
    }

    [SkippableTheory]
    [InlineData("CP999")]
    [InlineData("desconocido@ejemplo.com")]
    public async Task AuditLog_UnknownTarget_ExitsWithOne(string target)
    {
        SkipIfUnavailable();
        var manifest = WriteManifest(Manifest());

        (await RunAsync("audit-log", target, "--manifest", manifest)).Should().Be(CommandRunner.Failure);
    }

    [SkippableTheory]
    [InlineData("CP901", "--since", "ayer")]
    [InlineData("CP901", "--limit", "0")]
    [InlineData("CP901", "--action", "borrar_todo")]
    [InlineData("CP901", "CP902")]
    [InlineData("CP901", "--ip")]
    public async Task AuditLog_InvalidArguments_ExitsWithTwo(params string[] arguments)
    {
        SkipIfUnavailable();
        var manifest = WriteManifest(Manifest());

        (await RunAsync(["audit-log", .. arguments, "--manifest", manifest])).Should().Be(CommandRunner.Refused);
    }

    [SkippableFact]
    public async Task AuditLog_WithoutQaMode_ExitsWithTwo()
    {
        SkipIfUnavailable();
        var settings = new Dictionary<string, string?>(QaVariables) { ["QA_MODE"] = null };
        await using var factory = new CustomWebApplicationFactory(Postgres.ConnectionString, Redis.ConnectionString, additionalSettings: settings);

        (await RunCapturingAsync(factory, "audit-log", "CP901", "--manifest", WriteManifest(Manifest()))).Should().Be(CommandRunner.Refused);
    }

    private async Task<string> SeedAsync()
    {
        await SeedConsentDocumentAsync();
        var manifest = WriteManifest(Manifest());
        (await RunAsync("seed-qa", "--manifest", manifest)).Should().Be(CommandRunner.Success);
        return manifest;
    }

    private List<string> CommandLines() =>
        CapturedLog.Where(line => line.StartsWith("Cauce.Commands: ", StringComparison.Ordinal)).Select(line => line["Cauce.Commands: ".Length..]).ToList();

    private static AuditLog Row(Guid? actor, AuditActionType action, string entityType, Guid? entityId, string? context, string? hash, DateTime? at = null) =>
        AuditLog.Record(actor, action, entityType, entityId, hash, hash, IpAddress, UserAgent, context, at ?? DateTime.UtcNow);

    private async Task AddAuditAsync(params AuditLog[] rows)
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            db.AuditLogs.AddRange(rows);
            await db.SaveChangesAsync();
        }
    }

    private async Task<Guid> AddManualRecommendationAsync(Guid patientId, Guid nutritionistId)
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var recommendation = Recommendation.CreateManual(patientId, nutritionistId, "Manual", "Descripción", null, "Nota clínica de prueba.", null, DateTime.UtcNow);
            db.Recommendations.Add(recommendation);
            await db.SaveChangesAsync();
            return recommendation.Id;
        }
    }

    private async Task<Guid> UserIdAsync(string email)
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            return await db.Users.Where(user => user.Email == EmailNormalization.Normalize(email)).Select(user => user.Id).SingleAsync();
        }
    }

    private async Task<object> CountsAsync()
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            return new
            {
                Audit = await db.AuditLogs.CountAsync(),
                Users = await db.Users.AsNoTracking().OrderBy(user => user.Id).Select(user => new { user.Id, user.Status, user.UpdatedAt }).ToListAsync(),
                Recommendations = await db.Recommendations.CountAsync()
            };
        }
    }
}
