using Cauce.Application.Common.Interfaces.Identity;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Documento QuestPDF del comprobante de consentimiento informado del paciente (US01 CA04). Presenta el
/// encabezado con los datos de la aceptación (versión, momento, hash de integridad) y el texto íntegro
/// del documento aceptado. Marca visualmente que el contenido es un borrador en revisión clínica.
/// </summary>
public sealed class ConsentDocument : IDocument
{
    private readonly ConsentPdfContent _content;
    private readonly string _consentText;

    /// <summary>
    /// Inicializa el documento con los datos de la aceptación y el texto del consentimiento.
    /// </summary>
    /// <param name="content">Datos del encabezado y de la aceptación.</param>
    /// <param name="consentText">Texto íntegro del documento de consentimiento.</param>
    public ConsentDocument(ConsentPdfContent content, string consentText)
    {
        _content = content;
        _consentText = consentText;
    }

    /// <inheritdoc />
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    /// <inheritdoc />
    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(40);
            page.Size(PageSizes.A4);
            page.DefaultTextStyle(style => style.FontSize(10).FontColor(Colors.Grey.Darken4));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("Comprobante de Consentimiento Informado — Cauce")
                .FontSize(16).Bold().FontColor(Colors.Teal.Darken2);

            column.Item().PaddingTop(4).Background(Colors.Amber.Lighten4).Padding(6)
                .Text("Documento en revisión clínica (borrador). Pendiente de validación por el equipo Kaelín.")
                .FontSize(9).Bold().FontColor(Colors.Orange.Darken3);

            column.Item().PaddingTop(6).Text($"Paciente: {_content.PatientFullName}");
            column.Item().Text($"Correo: {_content.PatientEmail}");
            column.Item().Text($"Versión del documento: {_content.DocumentVersion}");
            column.Item().Text($"Aceptado el: {_content.AcceptedAtUtc:yyyy-MM-dd HH:mm} UTC");
            column.Item().Text($"IP de origen: {_content.IpAddress ?? "no registrada"}");
            column.Item().Text($"Hash SHA-256 del texto: {_content.ConsentTextHash}").FontSize(8).FontColor(Colors.Grey.Darken2);
            column.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(10).Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("Texto del consentimiento aceptado").FontSize(12).Bold().FontColor(Colors.Teal.Darken1);

            var paragraphs = _consentText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (paragraphs.Length == 0)
            {
                column.Item().Text(_consentText);
                return;
            }

            foreach (var paragraph in paragraphs)
            {
                column.Item().Text(paragraph.Trim()).LineHeight(1.3f);
            }
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text("Documento propio del paciente — Ley N.° 29733.")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
                row.ConstantItem(120).AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken1));
                    text.Span("Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        });
    }
}
