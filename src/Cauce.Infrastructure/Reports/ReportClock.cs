namespace Cauce.Infrastructure.Reports;

/// <summary>
/// Conversión entre UTC y la hora de Lima para el reporte. Perú no usa horario de verano, así que el
/// desfase es fijo.
/// </summary>
internal static class ReportClock
{
    private static readonly TimeSpan LimaOffset = TimeSpan.FromHours(-5);

    /// <summary>Hora de Lima de un instante UTC.</summary>
    public static DateTime ToLima(DateTime utc) => DateTime.SpecifyKind(utc + LimaOffset, DateTimeKind.Unspecified);

    /// <summary>Día de Lima de un instante UTC.</summary>
    public static DateOnly LimaDate(DateTime utc) => DateOnly.FromDateTime(ToLima(utc));

    /// <summary>Instante UTC en que empieza un día de Lima.</summary>
    public static DateTime StartOfLimaDayUtc(DateOnly day) =>
        DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue) - LimaOffset, DateTimeKind.Utc);
}
