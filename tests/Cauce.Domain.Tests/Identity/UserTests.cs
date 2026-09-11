using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;

namespace Cauce.Domain.Tests.Identity;

/// <summary>
/// Pruebas de invariantes y métodos de dominio de <see cref="User"/>.
/// </summary>
public sealed class UserTests
{
    private const int PatientRoleId = 1;
    private const int NutritionistRoleId = 2;

    [Fact]
    public void CreatePatient_ValidValues_CreatesPendingUnverifiedUser()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc-1", "p@cauce.local", "Paciente Uno", PatientRoleId);

        user.Status.Should().Be(UserStatus.PendingActivation);
        user.EmailVerified.Should().BeFalse();
        user.RoleId.Should().Be(PatientRoleId);
    }

    [Fact]
    public void CreateNutritionist_ValidValues_CreatesPendingVerifiedUser()
    {
        var user = User.CreateNutritionist(Guid.NewGuid(), "kc-2", "n@cauce.local", "Nutri Uno", NutritionistRoleId);

        // Nace pendiente: se activa recién al autenticarse por primera vez (acta A51).
        user.Status.Should().Be(UserStatus.PendingActivation);
        user.EmailVerified.Should().BeTrue();
    }

    [Fact]
    public void Activate_PendingNutritionist_BecomesActive()
    {
        var user = User.CreateNutritionist(Guid.NewGuid(), "kc-2", "n@cauce.local", "Nutri Uno", NutritionistRoleId);

        user.Activate();

        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void Activate_AlreadyActiveNutritionist_ThrowsInvalidOperation()
    {
        var user = User.CreateNutritionist(Guid.NewGuid(), "kc-2", "n@cauce.local", "Nutri Uno", NutritionistRoleId);
        user.Activate();

        var act = user.Activate;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreatePatient_EmptyEmail_ThrowsArgumentException()
    {
        var act = () => User.CreatePatient(Guid.NewGuid(), "kc-1", "", "Paciente", PatientRoleId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void VerifyEmail_PendingUser_ActivatesAndVerifies()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc-1", "p@cauce.local", "Paciente", PatientRoleId);

        user.VerifyEmail();

        user.EmailVerified.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void Activate_WithoutVerifiedEmail_ThrowsInvalidOperation()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc-1", "p@cauce.local", "Paciente", PatientRoleId);

        var act = user.Activate;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RegisterFailedLogin_FifthAttempt_LocksAccount()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc-1", "p@cauce.local", "Paciente", PatientRoleId);
        var now = DateTime.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            user.RegisterFailedLogin(now);
        }

        user.FailedLoginAttempts.Should().Be(5);
        user.IsLocked(now).Should().BeTrue();
    }

    [Fact]
    public void RegisterSuccessfulLogin_AfterFailures_ResetsCounterAndUnlocks()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc-1", "p@cauce.local", "Paciente", PatientRoleId);
        var now = DateTime.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            user.RegisterFailedLogin(now);
        }

        user.RegisterSuccessfulLogin(now);

        user.FailedLoginAttempts.Should().Be(0);
        user.IsLocked(now).Should().BeFalse();
        user.LastLoginAt.Should().Be(now);
    }

    [Fact]
    public void Suspend_ThenReactivate_ReturnsToActive()
    {
        var user = User.CreateNutritionist(Guid.NewGuid(), "kc-2", "n@cauce.local", "Nutri", NutritionistRoleId);

        user.Suspend();
        user.Status.Should().Be(UserStatus.Suspended);

        user.Reactivate();
        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void HasRole_MatchingCatalog_ReturnsTrue()
    {
        var user = User.CreateNutritionist(Guid.NewGuid(), "kc-2", "n@cauce.local", "Nutri", NutritionistRoleId);
        var catalog = new Dictionary<int, string> { [NutritionistRoleId] = UserRoles.Nutritionist };

        user.HasRole(UserRoles.Nutritionist, catalog).Should().BeTrue();
        user.HasRole(UserRoles.Patient, catalog).Should().BeFalse();
    }

    [Fact]
    public void Anonymize_PatientAccount_ReplacesPersonalDataAndDeactivates()
    {
        var id = Guid.NewGuid();
        var user = User.CreatePatient(id, "kc-1", "real@cauce.local", "Nombre Real", PatientRoleId);

        user.Anonymize(PatientRoleId);

        user.Email.Should().Be($"deleted-{id}@anonymized.local");
        user.FullName.Should().Be("Usuario anonimizado");
        user.Status.Should().Be(UserStatus.Inactive);
        user.FcmToken.Should().BeNull();
    }

    [Fact]
    public void Anonymize_NutritionistAccount_ThrowsOnlyPatientsCanBeAnonymized()
    {
        var user = User.CreateNutritionist(Guid.NewGuid(), "kc-2", "n@cauce.local", "Nutri", NutritionistRoleId);

        var act = () => user.Anonymize(PatientRoleId);

        act.Should().Throw<OnlyPatientsCanBeAnonymizedException>();
    }

    [Fact]
    public void EnrollInActivePilot_SetsFlagTrue()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc-1", "p@cauce.local", "Paciente", PatientRoleId);
        user.IsInActivePilot.Should().BeFalse();

        user.EnrollInActivePilot();

        user.IsInActivePilot.Should().BeTrue();
    }
}
