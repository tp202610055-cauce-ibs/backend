namespace Cauce.Domain.Common;

/// <summary>
/// Calendario civil de Lima. Perú no usa horario de verano, así que el desfase respecto de UTC es fijo.
/// Los instantes se reciben y se devuelven en UTC; solo la fecha civil se expresa en hora de Lima.
/// </summary>
public static class LimaCalendar
{
    /// <summary>
    /// Desfase de la hora de Lima respecto de UTC (cinco horas menos).
    /// </summary>
    public static readonly TimeSpan Offset = TimeSpan.FromHours(-5);

    /// <summary>
    /// Día de Lima al que pertenece un instante UTC.
    /// </summary>
    public static DateOnly DateOf(DateTime utc) => DateOnly.FromDateTime(utc + Offset);

    /// <summary>
    /// Instante UTC en que empieza un día de Lima, a las 00:00 de Lima.
    /// </summary>
    public static DateTime StartOfDayUtc(DateOnly day) =>
        DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue) - Offset, DateTimeKind.Utc);

    /// <summary>
    /// Instante UTC en que termina un día de Lima. Es un límite exclusivo: coincide con el inicio del día siguiente.
    /// </summary>
    public static DateTime EndOfDayUtc(DateOnly day) => StartOfDayUtc(day.AddDays(1));
}
