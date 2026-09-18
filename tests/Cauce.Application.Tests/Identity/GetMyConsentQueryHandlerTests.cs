using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.GetMyConsent;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;
using NSubstitute;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas de la consulta del consentimiento aceptado (HU0001 escenario 4, CP004 paso 2).
/// </summary>
/// <remarks>
/// La sección de privacidad del móvil muestra versión y fecha sin descargar el PDF, y decide con
/// <c>TextAvailable</c> si ofrecer la descarga. Ese campo es el que evita mandar al paciente contra
/// un 404: vale <c>false</c> justamente cuando el texto de la versión aceptada no quedó guardado.
/// </remarks>
public sealed class GetMyConsentQueryHandlerTests
{
    private const int PatientRoleId = 1;
    private const string AcceptedText = "Texto de la version 1.0, la que acepto el paciente.";

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IConsentRecordRepository _consentRecordRepository =
        Substitute.For<IConsentRecordRepository>();
    private readonly IConsentDocumentRepository _consentDocumentRepository =
        Substitute.For<IConsentDocumentRepository>();

    private GetMyConsentQueryHandler CreateHandler() => new(
        _currentUserService,
        _userRepository,
        _consentRecordRepository,
        _consentDocumentRepository);

    private User ArrangePatient(string acceptedVersion, string acceptedHash, DateTime acceptedAt)
    {
        var keycloakId = Guid.NewGuid();
        var user = User.CreatePatient(Guid.NewGuid(), keycloakId.ToString(), "p@cauce.local", "Paciente", PatientRoleId);

        _currentUserService.UserId.Returns(keycloakId);
        _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), Arg.Any<CancellationToken>())
            .Returns(user);
        _consentRecordRepository.FindCurrentForUserAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(ConsentRecord.Capture(
                Guid.NewGuid(), user.Id, acceptedVersion, acceptedHash, "127.0.0.1", acceptedAt));

        return user;
    }

    [Fact]
    public async Task Handle_ReturnsTheAcceptedVersionDateAndHash()
    {
        var acceptedAt = new DateTime(2026, 8, 5, 21, 44, 45, DateTimeKind.Utc);
        var document = ConsentDocument.Publish(Guid.NewGuid(), "1.0", AcceptedText, acceptedAt);
        ArrangePatient("1.0", document.TextHash, acceptedAt);
        _consentDocumentRepository.FindByVersionAsync("1.0", Arg.Any<CancellationToken>())
            .Returns(document);

        var result = await CreateHandler().Handle(new GetMyConsentQuery(), CancellationToken.None);

        result.DocumentVersion.Should().Be("1.0");
        result.AcceptedAt.Should().Be(acceptedAt);
        result.ConsentTextHash.Should().Be(document.TextHash);
    }

    [Fact]
    public async Task Handle_WithTheAcceptedVersionStored_ReportsTextAvailable()
    {
        var document = ConsentDocument.Publish(Guid.NewGuid(), "1.0", AcceptedText, DateTime.UtcNow);
        ArrangePatient("1.0", document.TextHash, DateTime.UtcNow);
        _consentDocumentRepository.FindByVersionAsync("1.0", Arg.Any<CancellationToken>())
            .Returns(document);

        var result = await CreateHandler().Handle(new GetMyConsentQuery(), CancellationToken.None);

        result.TextAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithoutTheAcceptedVersionStored_ReportsTextUnavailable()
    {
        // Aceptaciones anteriores a consent_documents. El PDF responde 404 para ellas, así que el
        // cliente necesita saberlo antes de ofrecer la descarga. La aceptación en sí sigue siendo
        // válida y se sigue informando con su versión y su fecha.
        ArrangePatient("0.9", new string('a', 64), DateTime.UtcNow);
        _consentDocumentRepository.FindByVersionAsync("0.9", Arg.Any<CancellationToken>())
            .Returns((ConsentDocument?)null);

        var result = await CreateHandler().Handle(new GetMyConsentQuery(), CancellationToken.None);

        result.TextAvailable.Should().BeFalse();
        result.DocumentVersion.Should().Be("0.9");
    }

    [Fact]
    public async Task Handle_WithoutConsentRecord_Throws()
    {
        var keycloakId = Guid.NewGuid();
        var user = User.CreatePatient(Guid.NewGuid(), keycloakId.ToString(), "p@cauce.local", "Paciente", PatientRoleId);
        _currentUserService.UserId.Returns(keycloakId);
        _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), Arg.Any<CancellationToken>())
            .Returns(user);
        _consentRecordRepository.FindCurrentForUserAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns((ConsentRecord?)null);

        var act = () => CreateHandler().Handle(new GetMyConsentQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<ConsentRecordNotFoundException>();
    }

    [Fact]
    public async Task Handle_WithoutAuthenticatedUser_Throws()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => CreateHandler().Handle(new GetMyConsentQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
