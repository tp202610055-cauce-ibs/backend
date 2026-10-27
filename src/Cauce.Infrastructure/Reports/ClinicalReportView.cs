namespace Cauce.Infrastructure.Reports;

/// <summary>
/// Contenido del reporte ya redactado en español y con horas de Lima, listo para dibujarse.
/// </summary>
/// <param name="Header">Líneas del encabezado, después del título.</param>
/// <param name="Sections">Secciones en orden de aparición.</param>
internal sealed record ClinicalReportView(IReadOnlyList<string> Header, IReadOnlyList<ReportViewSection> Sections);

/// <summary>Sección del reporte con su título y sus líneas.</summary>
/// <param name="Title">Título de la sección.</param>
/// <param name="Lines">Líneas de la sección.</param>
/// <param name="ShowsIbsSssChart">Indica si el gráfico de evolución IBS-SSS va al final de esta sección.</param>
internal sealed record ReportViewSection(string Title, IReadOnlyList<ReportViewLine> Lines, bool ShowsIbsSssChart = false);

/// <summary>Línea de una sección con su estilo.</summary>
/// <param name="Text">Texto de la línea.</param>
/// <param name="Kind">Estilo con que se dibuja.</param>
internal sealed record ReportViewLine(string Text, ReportLineKind Kind = ReportLineKind.Bullet);

/// <summary>Estilo de una línea del reporte.</summary>
internal enum ReportLineKind
{
    Text,
    Bullet,
    Subheading,
    Indented,
    Note
}
