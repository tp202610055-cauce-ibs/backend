using Cauce.Domain.Common;

namespace Cauce.Infrastructure.Reports;

/// <summary>
/// Conversión entre UTC y la hora de Lima para el reporte. Delega en <see cref="LimaCalendar"/>.
/// </summary>
internal static class ReportClock
{
    /// <summary>Hora de Lima de un instante UTC.</summary>
    public static DateTime ToLima(DateTime utc) => DateTime.SpecifyKind(utc + LimaCalendar.Offset, DateTimeKind.Unspecified);

    /// <summary>Día de Lima de un instante UTC.</summary>
    public static DateOnly LimaDate(DateTime utc) => LimaCalendar.DateOf(utc);

    /// <summary>Instante UTC en que empieza un día de Lima.</summary>
    public static DateTime StartOfLimaDayUtc(DateOnly day) => LimaCalendar.StartOfDayUtc(day);
}
