using System.Globalization;
using System.Text;

namespace Cauce.Infrastructure.Persistence.Seeders;

/// <summary>
/// Fila del manifiesto: un día de Lima con sus comidas (por fecha de consumo) y síntomas (por fecha de ocurrencia).
/// </summary>
public sealed record ManifestDay(DateOnly LimaDate, int Meals, int Symptoms);

/// <summary>
/// Resume un plan de volumen para compararlo con la pantalla de la app: totales, una fila por día de Lima,
/// grupos empatados, filas de hoy, filas registradas otro día y comidas con plato personalizado.
/// </summary>
public static class VolumeHistoryManifest
{
    private const int BusyDayThreshold = 8;

    /// <summary>
    /// Devuelve una fila por día de Lima, desde el más antiguo del historial hasta hoy, con los días vacíos.
    /// </summary>
    public static IReadOnlyList<ManifestDay> Days(VolumeHistoryPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var today = LimaDate(plan.SeededAtUtc);
        var mealsByDay = plan.Meals.GroupBy(meal => LimaDate(meal.ConsumedAt)).ToDictionary(group => group.Key, group => group.Count());
        var symptomsByDay = plan.Symptoms.GroupBy(symptom => LimaDate(symptom.OccurredAt)).ToDictionary(group => group.Key, group => group.Count());

        return Enumerable.Range(0, VolumeHistoryGenerator.HistoryDays + 1)
            .Select(offset => today.AddDays(offset - VolumeHistoryGenerator.HistoryDays))
            .Select(day => new ManifestDay(day, mealsByDay.GetValueOrDefault(day), symptomsByDay.GetValueOrDefault(day)))
            .ToList();
    }

    /// <summary>
    /// Construye el texto del manifiesto que se escribe en el log de desarrollo.
    /// </summary>
    public static string Build(VolumeHistoryPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var text = new StringBuilder();
        var today = LimaDate(plan.SeededAtUtc);
        var days = Days(plan);

        text.AppendLine(Invariant($"Volume patient manifest. Seeded at {plan.SeededAtUtc:yyyy-MM-dd HH:mm:ss}Z ({Lima(plan.SeededAtUtc):yyyy-MM-dd HH:mm} Lima)."));
        text.AppendLine(Invariant($"Totals: {plan.Meals.Count} meals, {plan.Symptoms.Count} symptoms, {plan.CustomFoods.Count} custom foods."));

        text.AppendLine("Per Lima day (meals by consumption date, symptoms by occurrence date):");
        foreach (var day in days)
        {
            var marker = day.Meals == 0 && day.Symptoms == 0 ? "  (empty)" : string.Empty;
            text.AppendLine(Invariant($"  {day.LimaDate:yyyy-MM-dd}  meals {day.Meals,2}  symptoms {day.Symptoms,2}{marker}"));
        }

        text.AppendLine("Days with 8 or more meals:");
        foreach (var day in days.Where(day => day.Meals >= BusyDayThreshold))
        {
            text.AppendLine(Invariant($"  {day.LimaDate:yyyy-MM-dd}  {day.Meals} meals"));
        }

        text.AppendLine("Tie groups (same clientCreatedAt, server order clientCreatedAt desc, id desc):");
        foreach (var group in plan.TieGroups)
        {
            text.AppendLine(Invariant(
                $"  {group.Kind,-8}  {group.ClientCreatedAt:yyyy-MM-dd HH:mm:ss}Z ({Lima(group.ClientCreatedAt):yyyy-MM-dd HH:mm} Lima)  {group.Count} rows at positions {group.FirstPosition}-{group.FirstPosition + group.Count - 1}"));
        }

        text.AppendLine("Today rows (Lima date of consumption or occurrence):");
        foreach (var meal in plan.Meals.Where(meal => LimaDate(meal.ConsumedAt) == today).OrderBy(meal => meal.ConsumedAt))
        {
            text.AppendLine(Invariant($"  meal     consumed {Lima(meal.ConsumedAt):HH:mm}  registered {Lima(meal.ClientCreatedAt):HH:mm}  {meal.MealTime}"));
        }

        foreach (var symptom in plan.Symptoms.Where(symptom => LimaDate(symptom.OccurredAt) == today).OrderBy(symptom => symptom.OccurredAt))
        {
            text.AppendLine(Invariant($"  symptom  occurred {Lima(symptom.OccurredAt):HH:mm}  registered {Lima(symptom.ClientCreatedAt):HH:mm}  {symptom.Type}"));
        }

        text.AppendLine("Rows registered on a different Lima day than consumed or occurred:");
        foreach (var meal in plan.Meals.Where(meal => LimaDate(meal.ClientCreatedAt) != LimaDate(meal.ConsumedAt)).OrderBy(meal => meal.ConsumedAt))
        {
            text.AppendLine(Invariant($"  meal     consumed {Lima(meal.ConsumedAt):yyyy-MM-dd HH:mm}  registered {Lima(meal.ClientCreatedAt):yyyy-MM-dd HH:mm}"));
        }

        foreach (var symptom in plan.Symptoms.Where(symptom => LimaDate(symptom.ClientCreatedAt) != LimaDate(symptom.OccurredAt)).OrderBy(symptom => symptom.OccurredAt))
        {
            text.AppendLine(Invariant($"  symptom  occurred {Lima(symptom.OccurredAt):yyyy-MM-dd HH:mm}  registered {Lima(symptom.ClientCreatedAt):yyyy-MM-dd HH:mm}"));
        }

        text.AppendLine("Meals with a custom food:");
        foreach (var meal in plan.Meals.Where(meal => meal.Items.Any(item => item.CustomFoodIndex is not null)).OrderBy(meal => meal.ConsumedAt))
        {
            var customOnly = meal.Items.All(item => item.CustomFoodIndex is not null) ? "  (custom food only)" : string.Empty;
            text.AppendLine(Invariant($"  {Lima(meal.ConsumedAt):yyyy-MM-dd HH:mm}  {meal.MealTime}{customOnly}"));
        }

        var oldest = plan.Meals.Select(meal => meal.ConsumedAt).Concat(plan.Symptoms.Select(symptom => symptom.OccurredAt)).Min();
        text.Append(Invariant($"Oldest row: {Lima(oldest):yyyy-MM-dd HH:mm} Lima."));
        return text.ToString();
    }

    private static DateTime Lima(DateTime utc) => VolumeHistoryGenerator.ToLima(utc);

    private static DateOnly LimaDate(DateTime utc) => DateOnly.FromDateTime(Lima(utc));

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
