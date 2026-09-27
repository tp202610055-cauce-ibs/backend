using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Infrastructure.Identity;
using Cauce.Infrastructure.Persistence.Seeders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas de las cuentas de nutricionista que siembra el entorno de desarrollo para los casos de prueba
/// del portal (acta A68): el demo con contraseña permanente, la deshabilitada y la pendiente. Requieren
/// Docker (PostgreSQL + Redis). En Testing la cadena de sembrado no corre, así que se invocan a mano.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DevelopmentAccountSeedersTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string InactiveEmail = "nutricionista.inactivo@cauce.local";
    private const string PendingEmail = "nutricionista.pendiente@cauce.local";

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis/KeyDB.</param>
    public DevelopmentAccountSeedersTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task QaSeeder_CreatesADisabledAndAPendingNutritionist()
    {
        SkipIfUnavailable();

        await RunQaSeederAsync();

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var inactive = await db.Users.AsNoTracking().SingleAsync(user => user.Email == InactiveEmail);
        var pending = await db.Users.AsNoTracking().SingleAsync(user => user.Email == PendingEmail);

        inactive.Status.Should().Be(UserStatus.Suspended);
        Factory.KeycloakClient.DisabledUsers.Should().Contain(inactive.KeycloakId);
        Factory.KeycloakClient.PasswordResets.Should().Contain(inactive.KeycloakId, "entra con la contraseña correcta y aun así falla");

        pending.Status.Should().Be(UserStatus.PendingActivation);
        Factory.KeycloakClient.PasswordResets.Should().NotContain(pending.KeycloakId);
        Factory.KeycloakClient.UpdatePasswordEmailsSent.Should().BeEmpty("no llena Mailpit en cada instalación");
    }

    [SkippableFact]
    public async Task QaSeeder_RunTwice_IsIdempotent()
    {
        SkipIfUnavailable();

        await RunQaSeederAsync();
        await RunQaSeederAsync();

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.Users.CountAsync(user => user.Email == InactiveEmail || user.Email == PendingEmail)).Should().Be(2);
    }

    [SkippableFact]
    public async Task QaSeeder_Disabled_DoesNothing()
    {
        SkipIfUnavailable();

        await RunQaSeederAsync(enabled: false);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.Users.AnyAsync(user => user.Email == InactiveEmail || user.Email == PendingEmail)).Should().BeFalse();
    }

    [SkippableFact]
    public async Task DevAdminSeeder_SetsAPermanentPasswordAndActivates()
    {
        SkipIfUnavailable();
        var options = Options.Create(new DevAdminOptions
        {
            Enabled = true,
            Email = "nutricionista.demo@cauce.local",
            FullName = "Dietista Demo",
            Password = "Portal#2026"
        });

        using (var scope = Factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var seeder = new DevAdminSeeder(
                services.GetRequiredService<IUserRepository>(),
                services.GetRequiredService<IKeycloakAdminClient>(),
                services.GetRequiredService<IUnitOfWork>(),
                options,
                NullLogger<DevAdminSeeder>.Instance);
            await seeder.SeedAsync();
        }

        var (dbScope, db) = CreateDbScope();
        using var _ = dbScope;
        var demo = await db.Users.AsNoTracking().SingleAsync(user => user.Email == "nutricionista.demo@cauce.local");
        demo.Status.Should().Be(UserStatus.Active);
        // Permanente: el portal entra por el backend y nunca muestra dónde cambiar una temporal (acta A68).
        Factory.KeycloakClient.PasswordResets.Should().Contain(demo.KeycloakId);
    }

    private async Task RunQaSeederAsync(bool enabled = true)
    {
        var options = Options.Create(new QaNutritionistsOptions
        {
            Enabled = enabled,
            Password = "Portal#2026",
            InactiveEmail = InactiveEmail,
            InactiveFullName = "Nutricionista Inactivo QA",
            PendingEmail = PendingEmail,
            PendingFullName = "Nutricionista Pendiente QA"
        });

        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var seeder = new QaNutritionistsSeeder(
            services.GetRequiredService<IUserRepository>(),
            services.GetRequiredService<IKeycloakAdminClient>(),
            services.GetRequiredService<IUnitOfWork>(),
            options,
            NullLogger<QaNutritionistsSeeder>.Instance);
        await seeder.SeedAsync();
    }
}
