namespace Cauce.Infrastructure.Qa.Manifest;

/// <summary>
/// Momento relativo del manifiesto: hace N horas, hace N días, hoy o una fecha fija de Lima.
/// </summary>
public sealed record QaOffset(int? Hours, int? Days, bool Today, DateOnly? Date)
{
    /// <summary>
    /// Desfase de un número de horas antes del ancla.
    /// </summary>
    public static QaOffset HoursAgo(int hours) => new(hours, null, false, null);

    /// <summary>
    /// Desfase de un número de días antes del ancla.
    /// </summary>
    public static QaOffset DaysAgo(int days) => new(null, days, false, null);
}

/// <summary>
/// Reloj de una ejecución: un único instante ancla del que salen todos los momentos relativos.
/// </summary>
public sealed class QaClock
{
    /// <summary>
    /// Desfase de Lima respecto de UTC. Perú no usa horario de verano.
    /// </summary>
    public static readonly TimeSpan LimaOffset = TimeSpan.FromHours(-5);

    /// <summary>
    /// Crea el reloj con el ancla indicada, que se normaliza a UTC.
    /// </summary>
    public QaClock(DateTimeOffset anchor)
    {
        Anchor = anchor.UtcDateTime;
    }

    /// <summary>
    /// Instante ancla de la ejecución, en UTC.
    /// </summary>
    public DateTime Anchor { get; }

    /// <summary>
    /// Día de Lima del ancla.
    /// </summary>
    public DateOnly LimaToday => DateOnly.FromDateTime(Anchor + LimaOffset);

    /// <summary>
    /// Medianoche de Lima del día del ancla, en UTC.
    /// </summary>
    public DateTime LimaTodayStartUtc => AtLimaTime(LimaToday, TimeOnly.MinValue);

    /// <summary>
    /// Convierte un desfase del manifiesto en un instante UTC. Una fecha fija se toma a las 12:00 de Lima.
    /// </summary>
    public DateTime Resolve(QaOffset offset)
    {
        ArgumentNullException.ThrowIfNull(offset);

        if (offset.Date is { } date)
        {
            return AtLimaTime(date, new TimeOnly(12, 0));
        }

        if (offset.Hours is { } hours)
        {
            return Anchor.AddHours(-hours);
        }

        if (offset.Days is { } days)
        {
            return Anchor.AddDays(-days);
        }

        return Anchor;
    }

    /// <summary>
    /// Instante UTC de una hora de Lima en un día dado.
    /// </summary>
    public static DateTime AtLimaTime(DateOnly day, TimeOnly time) =>
        DateTime.SpecifyKind(day.ToDateTime(time) - LimaOffset, DateTimeKind.Utc);
}
