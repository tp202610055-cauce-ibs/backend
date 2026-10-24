using Cauce.Infrastructure.Qa;
using Cauce.Infrastructure.Qa.Identity;
using Cauce.Infrastructure.Qa.Manifest;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Cauce.Infrastructure.Tests.Qa;

public sealed class QaAccountsUnitTests
{
    private const string RealmPolicy = "length(8) and digits(1) and upperCase(1) and lowerCase(1) and notUsername(undefined)";

    [Fact]
    public void FindViolations_WeakPassword_NamesTheRulesWithoutThePassword()
    {
        var violations = QaPasswordPolicy.FindViolations("corta", RealmPolicy, ["paciente.qa@ejemplo.com"]);

        violations.Should().BeEquivalentTo("length", "digits", "upperCase");
        violations.Should().NotContain(violation => violation.Contains("corta"));
    }

    [Fact]
    public void FindViolations_PasswordEqualToTheEmail_BreaksNotUsername()
    {
        QaPasswordPolicy.FindViolations("Paciente.Qa1@ejemplo.com", RealmPolicy, ["paciente.qa1@ejemplo.com"])
            .Should().Equal("notUsername");
    }

    [Fact]
    public void FindViolations_CompliantPasswordOrNoPolicy_IsEmpty()
    {
        QaPasswordPolicy.FindViolations("Sintetica1Paciente", RealmPolicy, ["paciente.qa@ejemplo.com"]).Should().BeEmpty();
        QaPasswordPolicy.FindViolations("x", null, []).Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, null, false, false)]
    [InlineData("1", null, true, false)]
    [InlineData("1", "1", true, true)]
    [InlineData("true", "1", false, false)]
    public void Guards_FollowTheQaVariables(string? mode, string? destructive, bool modeAllowed, bool destructiveAllowed)
    {
        var settings = new QaSettings(Configuration(new() { ["QA_MODE"] = mode, ["QA_ALLOW_DESTRUCTIVE"] = destructive }));

        if (modeAllowed)
        {
            ((Action)settings.EnsureQaMode).Should().NotThrow();
        }
        else
        {
            ((Action)settings.EnsureQaMode).Should().Throw<QaGuardException>();
        }

        if (destructiveAllowed)
        {
            ((Action)settings.EnsureDestructiveAllowed).Should().NotThrow();
        }
        else
        {
            ((Action)settings.EnsureDestructiveAllowed).Should().Throw<QaGuardException>();
        }
    }

    [Fact]
    public void PasswordFor_MissingVariable_NamesTheVariableOnly()
    {
        var settings = new QaSettings(Configuration(new() { ["QA_NUTRITIONIST_PASSWORD"] = "Sintetica2Nutri" }));

        settings.PasswordFor(QaRole.Nutritionist, smoke: false).Should().Be("Sintetica2Nutri");
        var act = () => settings.PasswordFor(QaRole.Patient, smoke: false);
        act.Should().Throw<QaGuardException>().WithMessage("*QA_PATIENT_PASSWORD*");
        var smoke = () => settings.PasswordFor(QaRole.Nutritionist, smoke: true);
        smoke.Should().Throw<QaGuardException>().WithMessage("*QA_SMOKE_PASSWORD*");
    }

    [Theory]
    [InlineData("paciente.perfil1@ejemplo.com", null, "Paciente Perfil1")]
    [InlineData("nutricionista.demo@ejemplo.com", "Ana Gómez", "Ana Gómez")]
    public void DisplayNameFor_UsesTheManifestNameOrTheEmail(string email, string? displayName, string expected)
    {
        QaDefaults.DisplayNameFor(email, displayName).Should().Be(expected);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
