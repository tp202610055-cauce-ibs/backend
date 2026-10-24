using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using FluentAssertions;

namespace Cauce.Domain.Tests.Identity;

public sealed class UserRetireNutritionistTests
{
    private const int PatientRoleId = 1;
    private const int NutritionistRoleId = 2;

    [Fact]
    public void RetireNutritionist_ActiveNutritionist_FreesTheEmailAndDeactivates()
    {
        var user = User.CreateNutritionist(Guid.NewGuid(), "kc-1", "nutricionista.qa@ejemplo.com", "Ana Gómez", NutritionistRoleId);
        user.Activate();
        user.RegisterFcmToken("token-de-prueba");

        user.RetireNutritionist(NutritionistRoleId);

        user.Email.Should().Be($"retired-{user.Id}@retired.local");
        user.Status.Should().Be(UserStatus.Inactive);
        user.FcmToken.Should().BeNull();
    }

    [Fact]
    public void RetireNutritionist_PendingActivation_AlsoRetires()
    {
        var user = User.CreateNutritionist(Guid.NewGuid(), "kc-2", "nutricionista.nuevo@ejemplo.com", "Nuevo", NutritionistRoleId);

        user.RetireNutritionist(NutritionistRoleId);

        user.Status.Should().Be(UserStatus.Inactive);
    }

    [Fact]
    public void RetireNutritionist_Patient_IsRejected()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc-3", "paciente.qa@ejemplo.com", "Paciente", PatientRoleId, PatientCode.FromCorrelative(1));

        var act = () => user.RetireNutritionist(NutritionistRoleId);

        act.Should().Throw<InvalidOperationException>();
        user.Email.Should().Be("paciente.qa@ejemplo.com");
    }
}
