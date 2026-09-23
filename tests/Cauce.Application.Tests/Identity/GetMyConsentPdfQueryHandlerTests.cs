using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.GetMyConsentPdf;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;
using NSubstitute;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del comprobante en PDF del consentimiento (HU0001 escenario 4, CP004 paso 7).
/// </summary>
/// <remarks>
/// El contrato de fondo que se verifica aquí es que el PDF reproduzca <b>el texto de la versión
/// que el paciente aceptó</b>, aunque exista una versión más nueva vigente. No se prueba sobre el
/// caso actual, donde solo existe la versión "1.0": los casos siembran dos versiones a propósito,
/// porque el defecto que esto previene solo aparece cuando difieren.
/// </remarks>
public sealed class GetMyConsentPdfQueryHandlerTests
{
    private const int PatientRoleId = 1;
    private const string OldText = "Texto de la version 1.0, la que acepto el paciente.";
    private const string NewText = "Texto de la version 2.0, vigente hoy y distinto del anterior.";

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IConsentRecordRepository _consentRecordRepository =
        Substitute.For<IConsentRecordRepository>();
    private readonly IConsentPdfRenderer _renderer = Substitute.For<IConsentPdfRenderer>();
    private readonly IConsentDocumentRepository _consentDocumentRepository =
        Substitute.For<IConsentDocumentRepository>();

    private GetMyConsentPdfQueryHandler CreateHandler() => new(
        _currentUserService,
        _userRepository,
        _consentRecordRepository,
        _renderer,
        _consentDocumentRepository);

    /// <summary>
    /// Deja el escenario listo: un paciente que aceptó <paramref name="acceptedVersion"/>.
    /// </summary>
    private User ArrangePatient(string acceptedVersion, string acceptedHash)
    {
        var keycloakId = Guid.NewGuid();
        var user = User.CreatePatient(Guid.NewGuid(), keycloakId.ToString(), "p@cauce.local", "Paciente", PatientRoleId, PatientCode.FromCorrelative(1));

        _currentUserService.UserId.Returns(keycloakId);
        _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), Arg.Any<CancellationToken>())
            .Returns(user);
        _consentRecordRepository.FindCurrentForUserAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(ConsentRecord.Capture(
                Guid.NewGuid(), user.Id, acceptedVersion, acceptedHash, "127.0.0.1", DateTime.UtcNow));

        _renderer.Render(Arg.Any<ConsentPdfContent>(), Arg.Any<string>()).Returns([1, 2, 3]);
        return user;
    }

    [Fact]
    public async Task Handle_WithNewerVersionPublished_RendersTheAcceptedOne()
    {
        // Existen dos versiones y el paciente acepto la vieja. El PDF tiene que llevar el texto
        // de la 1.0, no el de la 2.0 vigente.
        var oldDocument = ConsentDocument.Publish(
            Guid.NewGuid(), "1.0", OldText, DateTime.UtcNow.AddYears(-1), isCurrent: false);
        ArrangePatient("1.0", oldDocument.TextHash);
        _consentDocumentRepository.FindByVersionAsync("1.0", Arg.Any<CancellationToken>())
            .Returns(oldDocument);

        await CreateHandler().Handle(new GetMyConsentPdfQuery(), CancellationToken.None);

        await _consentDocumentRepository.Received(1).FindByVersionAsync("1.0", Arg.Any<CancellationToken>());
        _renderer.Received(1).Render(Arg.Any<ConsentPdfContent>(), OldText);
        _renderer.DidNotReceive().Render(Arg.Any<ConsentPdfContent>(), NewText);
    }

    [Fact]
    public async Task Handle_EachVersionRendersItsOwnText()
    {
        // La contraparte del caso anterior: quien acepto la 2.0 recibe el texto de la 2.0. Sin
        // esto, un test que solo mire la version vieja pasaria igual con un renderer que
        // devolviera siempre lo mismo.
        var newDocument = ConsentDocument.Publish(Guid.NewGuid(), "2.0", NewText, DateTime.UtcNow);
        ArrangePatient("2.0", newDocument.TextHash);
        _consentDocumentRepository.FindByVersionAsync("2.0", Arg.Any<CancellationToken>())
            .Returns(newDocument);

        await CreateHandler().Handle(new GetMyConsentPdfQuery(), CancellationToken.None);

        _renderer.Received(1).Render(Arg.Any<ConsentPdfContent>(), NewText);
        _renderer.DidNotReceive().Render(Arg.Any<ConsentPdfContent>(), OldText);
    }

    [Fact]
    public async Task Handle_FileNameCarriesTheAcceptedVersion()
    {
        var oldDocument = ConsentDocument.Publish(
            Guid.NewGuid(), "1.0", OldText, DateTime.UtcNow.AddYears(-1), isCurrent: false);
        ArrangePatient("1.0", oldDocument.TextHash);
        _consentDocumentRepository.FindByVersionAsync("1.0", Arg.Any<CancellationToken>())
            .Returns(oldDocument);

        var result = await CreateHandler().Handle(new GetMyConsentPdfQuery(), CancellationToken.None);

        result.FileName.Should().Be("consentimiento-1.0.pdf");
    }

    [Fact]
    public async Task Handle_PdfContentCarriesTheAcceptedHashAndVersion()
    {
        var oldDocument = ConsentDocument.Publish(
            Guid.NewGuid(), "1.0", OldText, DateTime.UtcNow.AddYears(-1), isCurrent: false);
        ArrangePatient("1.0", oldDocument.TextHash);
        _consentDocumentRepository.FindByVersionAsync("1.0", Arg.Any<CancellationToken>())
            .Returns(oldDocument);

        await CreateHandler().Handle(new GetMyConsentPdfQuery(), CancellationToken.None);

        // El hash impreso y el texto impreso tienen que pertenecer a la misma version, que es
        // justamente lo que el defecto anterior rompia.
        _renderer.Received(1).Render(
            Arg.Is<ConsentPdfContent>(c =>
                c.DocumentVersion == "1.0" && c.ConsentTextHash == oldDocument.TextHash),
            OldText);
    }

    [Fact]
    public async Task Handle_WhenAcceptedVersionTextIsMissing_Throws()
    {
        // Aceptaciones anteriores a la tabla: no hay texto que reproducir. Emitir un PDF con el
        // texto vigente seria peor que no emitirlo, porque pareceria autentico.
        ArrangePatient("0.9", new string('a', 64));
        _consentDocumentRepository.FindByVersionAsync("0.9", Arg.Any<CancellationToken>())
            .Returns((ConsentDocument?)null);

        var act = () => CreateHandler().Handle(new GetMyConsentPdfQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<ConsentRecordNotFoundException>();
        _renderer.DidNotReceive().Render(Arg.Any<ConsentPdfContent>(), Arg.Any<string>());
    }
}
