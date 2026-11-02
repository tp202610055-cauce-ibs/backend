using Cauce.Application.Reports.Contracts;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cauce.Infrastructure.Reports;

/// <summary>
/// Documento QuestPDF del reporte clínico del paciente. Dibuja el contenido que redacta
/// <see cref="ClinicalReportViewBuilder"/>, sin datos técnicos del sistema.
/// </summary>
public sealed class ClinicalReportDocument : IDocument
{
    private readonly ClinicalReportView _view;
    private readonly byte[]? _ibsSssChartPng;

    /// <summary>
    /// Inicializa el documento con los datos del reporte y, opcionalmente, el gráfico IBS-SSS en PNG.
    /// </summary>
    /// <param name="data">Datos del reporte.</param>
    /// <param name="ibsSssChartPng">PNG del gráfico de evolución IBS-SSS, o <see langword="null"/> si no aplica.</param>
    public ClinicalReportDocument(ClinicalReportData data, byte[]? ibsSssChartPng = null)
    {
        _view = ClinicalReportViewBuilder.Build(data);
        _ibsSssChartPng = ibsSssChartPng;
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
            column.Item().Text("Reporte clínico · Cauce").FontSize(18).Bold().FontColor(Colors.Teal.Darken2);
            foreach (var line in _view.Header)
            {
                column.Item().Text(line);
            }

            column.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(10).Column(column =>
        {
            column.Spacing(12);
            foreach (var section in _view.Sections)
            {
                column.Item().Element(c => ComposeSection(c, section));
            }
        });
    }

    private void ComposeSection(IContainer container, ReportViewSection section)
    {
        container.Column(column =>
        {
            column.Item().Text(section.Title).FontSize(12).Bold().FontColor(Colors.Teal.Darken1);
            column.Item().PaddingTop(2).Column(inner =>
            {
                foreach (var line in section.Lines)
                {
                    ComposeLine(inner, line);
                }

                if (section.ShowsIbsSssChart && _ibsSssChartPng is not null)
                {
                    inner.Item().PaddingTop(8).Image(_ibsSssChartPng).FitWidth();
                }
            });
        });
    }

    private static void ComposeLine(ColumnDescriptor column, ReportViewLine line)
    {
        switch (line.Kind)
        {
            case ReportLineKind.Subheading:
                column.Item().PaddingTop(4).Text(line.Text).SemiBold();
                break;
            case ReportLineKind.Indented:
                column.Item().PaddingLeft(10).Text(line.Text);
                break;
            case ReportLineKind.Note:
                column.Item().PaddingLeft(10).Text(line.Text).Italic().FontColor(Colors.Grey.Darken2);
                break;
            case ReportLineKind.Bullet:
                column.Item().Text($"• {line.Text}");
                break;
            default:
                column.Item().Text(line.Text);
                break;
        }
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text("Documento confidencial (Ley N.° 29733). No redistribuir.")
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
