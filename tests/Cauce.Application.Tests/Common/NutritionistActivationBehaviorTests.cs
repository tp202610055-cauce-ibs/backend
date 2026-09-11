using Cauce.Application.Common.Behaviors;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Cauce.Application.Tests.Common;

/// <summary>
/// Pruebas del <see cref="NutritionistActivationBehavior{TRequest,TResponse}"/> (acta A51): solo consulta
/// la base cuando el token es de un nutricionista, y confirma la activación antes de invocar al handler.
/// </summary>
public sealed class NutritionistActivationBehaviorTests
{
    private const int NutritionistRoleId = 2;

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly INutritionistActivationService _activationService = Substitute.For<INutritionistActivationService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private NutritionistActivationBehavior<PlainRequest, Unit> CreateBehavior() =>
        new(_currentUserService, _userRepository, _activationService, _unitOfWork);

    private User GivenAuthenticatedNutritionist()
    {
        var keycloakId = Guid.NewGuid();
        _currentUserService.UserId.Returns(keycloakId);
        _currentUserService.Roles.Returns([UserRoles.Nutritionist]);

        var user = User.CreateNutritionist(
            Guid.NewGuid(), keycloakId.ToString(), "n@cauce.local", "Nutri Demo", NutritionistRoleId);
        _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    [Fact]
    public async Task Handle_AnonymousRequest_SkipsWithoutQuerying()
    {
        _currentUserService.UserId.Returns((Guid?)null);
        var nextCalled = false;

        await CreateBehavior().Handle(
            new PlainRequest(),
            () =>
            {
                nextCalled = true;
                return Task.FromResult(Unit.Value);
            },
            CancellationToken.None);

        nextCalled.Should().BeTrue();
        await _userRepository.DidNotReceiveWithAnyArgs().FindByKeycloakIdAsync(default!, default);
    }

    [Fact]
    public async Task Handle_PatientToken_SkipsWithoutQuerying()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _currentUserService.Roles.Returns([UserRoles.Patient]);

        await CreateBehavior().Handle(new PlainRequest(), () => Task.FromResult(Unit.Value), CancellationToken.None);

        // La salida temprana se resuelve con los roles del token, sin tocar la base.
        await _userRepository.DidNotReceiveWithAnyArgs().FindByKeycloakIdAsync(default!, default);
    }

    [Fact]
    public async Task Handle_PendingNutritionist_CommitsTheActivationBeforeInvokingTheHandler()
    {
        var user = GivenAuthenticatedNutritionist();
        _activationService
            .ActivateIfPendingAsync(user, NutritionistActivationTrigger.AuthenticatedRequest, Arg.Any<CancellationToken>())
            .Returns(true);

        await CreateBehavior().Handle(
            new PlainRequest(),
            async () =>
            {
                // El handler de esta misma petición tiene que encontrar la activación ya confirmada.
                await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
                return Unit.Value;
            },
            CancellationToken.None);

        await _activationService.Received(1).ActivateIfPendingAsync(
            user, NutritionistActivationTrigger.AuthenticatedRequest, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ActivationNotNeeded_DoesNotCommit()
    {
        GivenAuthenticatedNutritionist();
        _activationService
            .ActivateIfPendingAsync(Arg.Any<User>(), Arg.Any<NutritionistActivationTrigger>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await CreateBehavior().Handle(new PlainRequest(), () => Task.FromResult(Unit.Value), CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NutritionistWithoutLocalAccount_SkipsActivationAndInvokesTheHandler()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _currentUserService.Roles.Returns([UserRoles.Nutritionist]);
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        var nextCalled = false;

        await CreateBehavior().Handle(
            new PlainRequest(),
            () =>
            {
                nextCalled = true;
                return Task.FromResult(Unit.Value);
            },
            CancellationToken.None);

        // La cuenta faltante la reporta el handler con su propio error; el behavior no la inventa.
        nextCalled.Should().BeTrue();
        await _activationService.DidNotReceiveWithAnyArgs().ActivateIfPendingAsync(default!, default, default);
    }

    private sealed record PlainRequest : IRequest<Unit>;
}
