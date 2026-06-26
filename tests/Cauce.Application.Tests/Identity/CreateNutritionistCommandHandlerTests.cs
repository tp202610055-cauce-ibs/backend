using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.CreateNutritionist;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del handler <see cref="CreateNutritionistCommandHandler"/>.
/// </summary>
public sealed class CreateNutritionistCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IKeycloakAdminClient _keycloakAdminClient = Substitute.For<IKeycloakAdminClient>();
    private readonly ITemporaryPasswordGenerator _passwordGenerator = Substitute.For<ITemporaryPasswordGenerator>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly ILogger<CreateNutritionistCommandHandler> _logger =
        Substitute.For<ILogger<CreateNutritionistCommandHandler>>();

    private CreateNutritionistCommandHandler CreateHandler() => new(
        _userRepository,
        _keycloakAdminClient,
        _passwordGenerator,
        _emailSender,
        _unitOfWork,
        _auditLogger,
        _logger);

    [Fact]
    public async Task Handle_ValidRequest_CreatesNutritionistAndSendsCredentials()
    {
        _userRepository.ExistsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, Arg.Any<CancellationToken>()).Returns(2);
        _passwordGenerator.Generate().Returns("TempPass1!");
        _keycloakAdminClient
            .CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), UserRoles.Nutritionist, false, Arg.Any<CancellationToken>())
            .Returns("kc-nutri");

        var result = await CreateHandler().Handle(
            new CreateNutritionistCommand("n@cauce.local", "Nutri Uno"), CancellationToken.None);

        result.TemporaryCredentialsEmailSent.Should().BeTrue();
        await _keycloakAdminClient.Received(1).SetTemporaryPasswordAsync("kc-nutri", "TempPass1!", Arg.Any<CancellationToken>());
        await _emailSender.Received(1).SendNutritionistTemporaryCredentialsAsync(
            "n@cauce.local", "Nutri Uno", "TempPass1!", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsDuplicateEmailException()
    {
        _userRepository.ExistsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var act = () => CreateHandler().Handle(new CreateNutritionistCommand("n@cauce.local", "Nutri"), CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateEmailException>();
    }

    [Fact]
    public async Task Handle_PersistenceFails_CompensatesByDeletingKeycloakUser()
    {
        _userRepository.ExistsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, Arg.Any<CancellationToken>()).Returns(2);
        _passwordGenerator.Generate().Returns("TempPass1!");
        _keycloakAdminClient
            .CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), UserRoles.Nutritionist, false, Arg.Any<CancellationToken>())
            .Returns("kc-nutri");
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("db down"));

        var act = () => CreateHandler().Handle(new CreateNutritionistCommand("n@cauce.local", "Nutri"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _keycloakAdminClient.Received(1).DeleteUserAsync("kc-nutri", Arg.Any<CancellationToken>());
    }
}
