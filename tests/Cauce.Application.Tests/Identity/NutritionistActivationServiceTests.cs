using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.Services;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del <see cref="NutritionistActivationService"/> (acta A51): activa solo a nutricionistas
/// pendientes, audita la transición y no hace nada en cualquier otro caso.
/// </summary>
public sealed class NutritionistActivationServiceTests
{
    private const int PatientRoleId = 1;
    private const int NutritionistRoleId = 2;

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly ILogger<NutritionistActivationService> _logger =
        Substitute.For<ILogger<NutritionistActivationService>>();

    private NutritionistActivationService CreateService()
    {
        _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, Arg.Any<CancellationToken>()).Returns(NutritionistRoleId);
        return new NutritionistActivationService(_userRepository, _auditLogger, _logger);
    }

    private static User PendingNutritionist() =>
        User.CreateNutritionist(Guid.NewGuid(), "kc-nutri", "n@cauce.local", "Nutri Demo", NutritionistRoleId);

    [Fact]
    public async Task ActivateIfPendingAsync_PendingNutritionist_ActivatesAndReturnsTrue()
    {
        var user = PendingNutritionist();

        var activated = await CreateService()
            .ActivateIfPendingAsync(user, NutritionistActivationTrigger.Login, CancellationToken.None);

        activated.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);
    }

    [Theory]
    [InlineData(NutritionistActivationTrigger.Login, "login")]
    [InlineData(NutritionistActivationTrigger.AuthenticatedRequest, "authenticated_request")]
    public async Task ActivateIfPendingAsync_PendingNutritionist_AuditsTheTriggerWithTheNutritionistAsActor(
        NutritionistActivationTrigger trigger,
        string expectedWireValue)
    {
        var user = PendingNutritionist();

        await CreateService().ActivateIfPendingAsync(user, trigger, CancellationToken.None);

        // Todos los argumentos string llevan especificación: NSubstitute no puede mezclar un null literal
        // con un Arg.Is del mismo tipo sin volverse ambiguo.
        await _auditLogger.Received(1).LogAsync(
            AuditActionType.AccountActivation,
            Arg.Is(nameof(User)),
            user.Id,
            Arg.Is<string?>(hash => hash == null),
            Arg.Is<string?>(hash => hash == null),
            Arg.Is<string?>(context => context!.Contains($"\"trigger\":\"{expectedWireValue}\"")),
            user.Id,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActivateIfPendingAsync_ActiveNutritionist_ReturnsFalseWithoutQueryingOrAuditing()
    {
        var user = PendingNutritionist();
        user.Activate();

        var activated = await CreateService()
            .ActivateIfPendingAsync(user, NutritionistActivationTrigger.AuthenticatedRequest, CancellationToken.None);

        activated.Should().BeFalse();
        // El caso frecuente es la cuenta ya activa: no debe pagar la consulta del rol.
        await _userRepository.DidNotReceiveWithAnyArgs().GetRoleIdAsync(default!, default);
        await _auditLogger.DidNotReceiveWithAnyArgs()
            .LogAsync(default, default!, default, default, default, default, default, default);
    }

    [Fact]
    public async Task ActivateIfPendingAsync_PendingPatient_LeavesItPending()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc-patient", "p@cauce.local", "Paciente", PatientRoleId);

        var activated = await CreateService()
            .ActivateIfPendingAsync(user, NutritionistActivationTrigger.Login, CancellationToken.None);

        // El paciente se activa al verificar su correo, no por autenticarse.
        activated.Should().BeFalse();
        user.Status.Should().Be(UserStatus.PendingActivation);
        await _auditLogger.DidNotReceiveWithAnyArgs()
            .LogAsync(default, default!, default, default, default, default, default, default);
    }

    [Fact]
    public async Task ActivateIfPendingAsync_PendingNutritionistWithUnverifiedEmail_SkipsWithoutThrowing()
    {
        // La fábrica de nutricionista siempre da el correo por verificado, así que este estado solo se
        // alcanza con la de paciente apuntando al rol nutricionista. Cubre la rama defensiva.
        var user = User.CreatePatient(Guid.NewGuid(), "kc-nutri", "n@cauce.local", "Nutri Demo", NutritionistRoleId);

        var activated = await CreateService()
            .ActivateIfPendingAsync(user, NutritionistActivationTrigger.Login, CancellationToken.None);

        activated.Should().BeFalse();
        user.Status.Should().Be(UserStatus.PendingActivation);
    }

    [Fact]
    public async Task ActivateIfPendingAsync_NullUser_ThrowsArgumentNullException()
    {
        var act = () => CreateService()
            .ActivateIfPendingAsync(null!, NutritionistActivationTrigger.Login, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
