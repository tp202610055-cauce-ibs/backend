using Cauce.Application.Common.Interfaces.Identity;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;

namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Implementación de <see cref="IConsentPdfRenderer"/> basada en QuestPDF. Toma el texto del documento
/// de consentimiento vigente de la configuración (<see cref="ConsentDocumentOptions"/>) y produce un
/// PDF sin cifrar con el comprobante de aceptación del paciente (US01 CA04).
/// </summary>
public sealed class QuestPdfConsentRenderer : IConsentPdfRenderer
{
    private readonly ConsentDocumentOptions _options;

    /// <summary>
    /// Inicializa el renderizador con las opciones del documento de consentimiento.
    /// </summary>
    /// <param name="options">Opciones del documento de consentimiento vigente.</param>
    public QuestPdfConsentRenderer(IOptions<ConsentDocumentOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public byte[] Render(ConsentPdfContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var document = new ConsentDocument(content, _options.Text);
        return document.GeneratePdf();
    }
}
