using Cauce.Application.Common.Interfaces.Identity;
using QuestPDF.Fluent;

namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Implementación de <see cref="IConsentPdfRenderer"/> basada en QuestPDF. Toma el texto del documento
/// de consentimiento que le entrega el handler, ya resuelto por version aceptada, y produce un
/// PDF sin cifrar con el comprobante de aceptación del paciente (US01 CA04).
/// </summary>
public sealed class QuestPdfConsentRenderer : IConsentPdfRenderer
{

    /// <inheritdoc />
    public byte[] Render(ConsentPdfContent content, string consentText)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(consentText);
        var document = new ConsentDocument(content, consentText);
        return document.GeneratePdf();
    }
}
