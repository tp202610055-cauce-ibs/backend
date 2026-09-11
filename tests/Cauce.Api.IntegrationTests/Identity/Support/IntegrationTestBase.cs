using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cauce.Domain.Auditing;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Persistence.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.Identity.Support;

/// <summary>
/// Base común de las pruebas de integración de la API. Levanta la fábrica contra los contenedores
/// efímeros, aplica migraciones, siembra los catálogos una vez y resetea los datos transaccionales
/// antes de cada test (con triggers desactivados, ver <see cref="DatabaseReset"/>). Ofrece helpers de
/// siembra de usuarios, autenticación y consulta de la bitácora de auditoría.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    /// <summary>
    /// Fixture del contenedor PostgreSQL efímero.
    /// </summary>
    protected PostgresFixture Postgres { get; }

    /// <summary>
    /// Fixture del contenedor Redis efímero.
    /// </summary>
    protected RedisFixture Redis { get; }

    /// <summary>
    /// Fábrica de la aplicación bajo prueba.
    /// </summary>
    protected CustomWebApplicationFactory Factory { get; private set; } = null!;

    /// <summary>
    /// Inicializa la base con los fixtures compartidos.
    /// </summary>
    /// <param name="postgres">Fixture de PostgreSQL.</param>
    /// <param name="redis">Fixture de Redis.</param>
    protected IntegrationTestBase(PostgresFixture postgres, RedisFixture redis)
    {
        Postgres = postgres;
        Redis = redis;
    }

    /// <summary>
    /// Indica si toda la infraestructura requerida está disponible.
    /// </summary>
    protected bool Available => Postgres.IsAvailable && Redis.IsAvailable && ExtraAvailable;

    /// <summary>
    /// Disponibilidad de contenedores adicionales requeridos por la clase derivada (MinIO/Mailpit).
    /// </summary>
    protected virtual bool ExtraAvailable => true;

    /// <summary>
    /// Crea la fábrica de la aplicación. Las clases derivadas la redefinen para inyectar MinIO/Mailpit.
    /// </summary>
    /// <returns>La fábrica de la aplicación.</returns>
    protected virtual CustomWebApplicationFactory CreateFactory() =>
        new(Postgres.ConnectionString, Redis.ConnectionString);

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        if (!Available)
        {
            return;
        }

        Factory = CreateFactory();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        await db.Database.MigrateAsync();

        await scope.ServiceProvider.GetRequiredService<UserRolesSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<AllergiesSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<FoodItemsSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<RecommendationsModelVersionsSeeder>().SeedAsync();

        await DatabaseReset.ResetTransactionalDataAsync(db);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }
    }

    /// <summary>
    /// Omite la prueba si la infraestructura no está disponible.
    /// </summary>
    protected void SkipIfUnavailable()
    {
        Skip.IfNot(Available, "Docker (contenedores efímeros) no disponible; se omite.");
    }

    /// <summary>
    /// Crea un ámbito con el contexto de base de datos.
    /// </summary>
    /// <returns>El ámbito y el contexto.</returns>
    protected (IServiceScope Scope, CauceDbContext Db) CreateDbScope()
    {
        var scope = Factory.Services.CreateScope();
        return (scope, scope.ServiceProvider.GetRequiredService<CauceDbContext>());
    }

    // ----- Siembra -----

    /// <summary>
    /// Siembra un paciente y devuelve su identificador local y de Keycloak.
    /// </summary>
    protected async Task<(Guid Id, string KeycloakId, string Email)> SeedPatientAsync()
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var roleId = await db.UserRoles.Where(r => r.RoleName == UserRoles.Patient).Select(r => r.RoleId).FirstAsync();
        var keycloakId = Guid.NewGuid().ToString();
        var email = $"patient-{Guid.NewGuid():N}@cauce.local";
        var user = User.CreatePatient(Guid.NewGuid(), keycloakId, email, "Paciente", roleId);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, keycloakId, email);
    }

    /// <summary>
    /// Siembra un nutricionista y devuelve su identificador local y de Keycloak.
    /// </summary>
    /// <param name="active">
    /// Si se deja operativo. La fábrica lo crea pendiente de activación (acta A51), y casi todas las
    /// pruebas necesitan uno que ya pueda atender, así que por defecto se activa.
    /// </param>
    protected async Task<(Guid Id, string KeycloakId, string Email)> SeedNutritionistAsync(bool active = true)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var roleId = await db.UserRoles.Where(r => r.RoleName == UserRoles.Nutritionist).Select(r => r.RoleId).FirstAsync();
        var keycloakId = Guid.NewGuid().ToString();
        var email = $"nutri-{Guid.NewGuid():N}@cauce.local";
        var user = User.CreateNutritionist(Guid.NewGuid(), keycloakId, email, "Nutri", roleId);
        if (active)
        {
            user.Activate();
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, keycloakId, email);
    }

    /// <summary>
    /// Siembra el perfil clínico de un paciente directamente.
    /// </summary>
    protected async Task SeedPatientProfileAsync(Guid userId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var dob = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30);
        var profile = PatientProfile.Create(
            Guid.NewGuid(), userId, dob, BiologicalSex.Male, 70m, 175m, IbsSubtype.IbsM, null, null, DateTime.UtcNow);
        db.PatientProfiles.Add(profile);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Devuelve el identificador de una alergia activa del catálogo.
    /// </summary>
    protected async Task<Guid> FirstActiveAllergyIdAsync()
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.Allergies.AsNoTracking().Where(a => a.IsActive).Select(a => a.Id).FirstAsync();
    }

    /// <summary>
    /// Establece la asignación nutricionista-paciente.
    /// </summary>
    protected async Task AssignAsync(Guid nutritionistId, Guid patientId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        db.NutritionistPatients.Add(NutritionistPatient.Establish(Guid.NewGuid(), nutritionistId, patientId, null, DateTime.UtcNow));
        await db.SaveChangesAsync();
    }

    // ----- Autenticación -----

    /// <summary>
    /// Cliente autenticado como paciente.
    /// </summary>
    protected HttpClient PatientClient(string keycloakId, string email = "patient@cauce.local") =>
        AuthedClient(keycloakId, email, UserRoles.Patient);

    /// <summary>
    /// Cliente autenticado como nutricionista.
    /// </summary>
    protected HttpClient NutritionistClient(string keycloakId, string email = "nutri@cauce.local") =>
        AuthedClient(keycloakId, email, UserRoles.Nutritionist);

    /// <summary>
    /// Cliente HTTP con un JWT de prueba con el rol indicado.
    /// </summary>
    protected HttpClient AuthedClient(string keycloakId, string email, string role)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtBuilder.Build(keycloakId, email, role));
        return client;
    }

    /// <summary>
    /// Envía un POST con header <c>Idempotency-Key</c> y cuerpo JSON opcional.
    /// </summary>
    protected static async Task<HttpResponseMessage> PostWithKey(HttpClient client, string url, object? content, Guid? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString());
        if (content is not null)
        {
            request.Content = JsonContent.Create(content);
        }

        return await client.SendAsync(request);
    }

    // ----- Auditoría -----

    /// <summary>
    /// Devuelve las filas de <c>audit_logs</c> del tipo de entidad y (opcionalmente) el identificador
    /// indicados, ordenadas por ocurrencia.
    /// </summary>
    protected async Task<IReadOnlyList<AuditLog>> AuditLogsAsync(string? entityType = null, Guid? entityId = null, AuditActionType? action = null)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var query = db.Set<AuditLog>().AsNoTracking().AsQueryable();
        if (entityType is not null)
        {
            query = query.Where(x => x.EntityType == entityType);
        }

        if (entityId is not null)
        {
            query = query.Where(x => x.EntityId == entityId);
        }

        if (action is not null)
        {
            query = query.Where(x => x.ActionType == action);
        }

        return await query.OrderBy(x => x.OccurredAt).ToListAsync();
    }
}
