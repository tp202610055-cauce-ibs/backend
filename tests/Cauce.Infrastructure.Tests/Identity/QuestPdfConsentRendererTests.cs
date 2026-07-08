using System.Text;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;

namespace Cauce.Infrastructure.Tests.Identity;

/// <summary>
/// Pruebas del renderizador QuestPDF del comprobante de consentimiento (US01 CA04): produce un PDF
/// válido y no cifrado a partir de los datos de la aceptación.
/// </summary>
public sealed class QuestPdfConsentRendererTests
{
    static QuestPdfConsentRendererTests()
    {
        // La licencia se fija en Program.cs en producción; en un unit test puro debe configurarse aquí.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void Render_ValidContent_ReturnsNonEmptyPdfBytes()
    {
        var options = Options.Create(new ConsentDocumentOptions
        {
            CurrentVersion = "1.0",
            Text = "Texto de consentimiento de prueba.\nSegundo párrafo del consentimiento."
        });
        var renderer = new QuestPdfConsentRenderer(options);
        var content = new ConsentPdfContent(
            "Paciente Prueba", "paciente@cauce.local", "1.0", DateTime.UtcNow, new string('a', 64), "127.0.0.1");

        var pdf = renderer.Render(content);

        pdf.Should().NotBeNullOrEmpty();
        Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }
}
