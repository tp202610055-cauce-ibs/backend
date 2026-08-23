using Cauce.Application.Common.Identity;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.RequestPasswordReset;
using Cauce.Domain.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del handler <see cref="RequestPasswordResetCommandHandler"/>.
/// </summary>
public sealed class RequestPasswordResetCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordResetTokenGenerator _tokenGenerator = Substitute.For<IPasswordResetTokenGenerator>();
    private readonly IPasswordResetTokenRepository _tokenRepository = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IClientUrlProvider _clientUrlProvider = Substitute.For<IClientUrlProvider>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<RequestPasswordResetCommandHandler> _logger =
        Substitute.For<ILogger<RequestPasswordResetCommandHandler>>();

    private RequestPasswordResetCommandHandler CreateHandler() => new(
        _userRepository,
        _tokenGenerator,
        _tokenRepository,
        _clientUrlProvider,
        _emailSender,
        _unitOfWork,
        _logger);

    [Fact]
    public async Task Handle_ExistingEmail_CreatesTokenAndSendsEmail()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc-1", "p@cauce.local", "Paciente", 1);
        _userRepository.FindByEmailAsync("p@cauce.local", Arg.Any<CancellationToken>()).Returns(user);
        _tokenGenerator.GeneratePair().Returns(("plain", "hash"));
        _clientUrlProvider.BuildPasswordResetLink("plain", OidcClients.Mobile).Returns("cauce://auth/password-reset?token=plain");

        await CreateHandler().Handle(new RequestPasswordResetCommand("p@cauce.local", null), CancellationToken.None);

        await _tokenRepository.Received(1).AddAsync(Arg.Any<PasswordResetToken>(), Arg.Any<CancellationToken>());
        await _emailSender.Received(1).SendPasswordResetLinkAsync(
            "p@cauce.local", "Paciente", "cauce://auth/password-reset?token=plain", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonExistentEmail_DoesNothing()
    {
        _userRepository.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        await CreateHandler().Handle(new RequestPasswordResetCommand("missing@cauce.local", null), CancellationToken.None);

        await _tokenRepository.DidNotReceive().AddAsync(Arg.Any<PasswordResetToken>(), Arg.Any<CancellationToken>());
        await _emailSender.DidNotReceive().SendPasswordResetLinkAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithoutClientId_UsesMobileAsDefault()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc-1", "p@cauce.local", "Paciente", 1);
        _userRepository.FindByEmailAsync("p@cauce.local", Arg.Any<CancellationToken>()).Returns(user);
        _tokenGenerator.GeneratePair().Returns(("plain", "hash"));

        await CreateHandler().Handle(
            new RequestPasswordResetCommand("p@cauce.local", null, null), CancellationToken.None);

        _clientUrlProvider.Received(1).BuildPasswordResetLink("plain", OidcClients.Mobile);
    }

    [Fact]
    public async Task Handle_WithPortalClientId_PropagatesItToTheUrlProvider()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc-1", "p@cauce.local", "Paciente", 1);
        _userRepository.FindByEmailAsync("p@cauce.local", Arg.Any<CancellationToken>()).Returns(user);
        _tokenGenerator.GeneratePair().Returns(("plain", "hash"));

        await CreateHandler().Handle(
            new RequestPasswordResetCommand("p@cauce.local", null, OidcClients.WebPortal),
            CancellationToken.None);

        _clientUrlProvider.Received(1).BuildPasswordResetLink("plain", OidcClients.WebPortal);
        _clientUrlProvider.DidNotReceive().BuildPasswordResetLink("plain", OidcClients.Mobile);
    }
}
