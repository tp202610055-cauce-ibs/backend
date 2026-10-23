using Cauce.Api.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Cauce.Api.IntegrationTests.Configuration;

/// <summary>
/// Pruebas unitarias de <see cref="ProductionSettingsValidator"/>: nombra las claves que faltan, nunca sus
/// valores, y no exige la credencial de Firebase. No necesitan Docker.
/// </summary>
public sealed class ProductionSettingsValidatorTests
{
    private const string SecretValue = "valor-que-no-debe-aparecer";

    private static Dictionary<string, string?> CompleteSettings() =>
        ProductionSettingsValidator.RequiredKeys.ToDictionary(key => key, _ => (string?)SecretValue);

    private static IConfiguration Build(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    [Fact]
    public void FindProblems_CompleteSettingsWithFakePush_ReturnsNone()
    {
        ProductionSettingsValidator.FindProblems(Build(CompleteSettings())).Should().BeEmpty();
    }

    [Fact]
    public void EnsureValid_MissingAndBlankKeys_NamesThemAsEnvironmentVariablesWithoutValues()
    {
        var settings = CompleteSettings();
        settings.Remove("Keycloak:ClientSecret");
        settings["Storage:Minio:PublicEndpoint"] = "   ";

        var act = () => ProductionSettingsValidator.EnsureValid(Build(settings));

        var message = act.Should().Throw<InvalidOperationException>().Which.Message;
        message.Should().Contain("Keycloak__ClientSecret").And.Contain("Storage__Minio__PublicEndpoint");
        message.Should().NotContain(SecretValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/run/secrets/no-existe.json")]
    public void FindProblems_RealPushWithoutReadableCredentials_DoesNotStopStartup(string? credentialsPath)
    {
        var settings = CompleteSettings();
        settings["Notifications:Fcm:UseFake"] = "false";
        settings["Notifications:Fcm:CredentialsPath"] = credentialsPath;

        ProductionSettingsValidator.FindProblems(Build(settings)).Should().BeEmpty();
    }
}
