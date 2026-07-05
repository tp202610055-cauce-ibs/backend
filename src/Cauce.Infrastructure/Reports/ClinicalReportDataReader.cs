using Cauce.Application.Common.Interfaces.Reports;
using Cauce.Application.Reports.Contracts;
using Cauce.Domain.Patients.Exceptions;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Reports;

/// <summary>
/// Implementación de <see cref="IClinicalReportDataReader"/> que consolida los datos clínicos del
/// paciente en un período desde <see cref="CauceDbContext"/>. Materializa los conjuntos acotados y
/// agrupa en memoria para evitar traducciones LINQ frágiles. No expone datos técnicos del sistema.
/// </summary>
public sealed class ClinicalReportDataReader : IClinicalReportDataReader
{
    private static readonly RecommendationStatus[] ApprovedStatuses =
    [
        RecommendationStatus.Approved,
        RecommendationStatus.Delivered,
        RecommendationStatus.FeedbackReceived
    ];

    private readonly CauceDbContext _context;
    private readonly ReportOptions _options;

    /// <summary>
    /// Inicializa el lector con el contexto de base de datos y las opciones de reporte.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    /// <param name="options">Opciones de reporte.</param>
    public ClinicalReportDataReader(CauceDbContext context, IOptions<ReportOptions> options)
    {
        _context = context;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<bool> HasDataInPeriodAsync(
        Guid patientId,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken ct = default)
    {
        var (start, end) = ToUtcRange(periodStart, periodEnd);

        var hasMeal = await _context.Meals
            .AsNoTracking()
            .AnyAsync(meal => meal.PatientId == patientId && meal.ConsumedAt >= start && meal.ConsumedAt <= end, ct)
            .ConfigureAwait(false);
        if (hasMeal)
        {
            return true;
        }

        return await _context.Symptoms
            .AsNoTracking()
            .AnyAsync(symptom => symptom.PatientId == patientId && symptom.OccurredAt >= start && symptom.OccurredAt <= end, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ClinicalReportData> GetReportDataAsync(
        Guid patientId,
        DateOnly periodStart,
        DateOnly periodEnd,
        Guid nutritionistId,
        CancellationToken ct = default)
    {
        var (start, end) = ToUtcRange(periodStart, periodEnd);

        var profile = await _context.PatientProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == patientId, ct)
            .ConfigureAwait(false)
            ?? throw new PatientProfileNotFoundException();

        var patientName = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == patientId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false) ?? string.Empty;

        var nutritionistName = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == nutritionistId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false) ?? string.Empty;

        var allergies = await (from patientAllergy in _context.PatientAllergies.AsNoTracking()
                               where patientAllergy.PatientId == patientId
                               join allergy in _context.Allergies.AsNoTracking()
                                   on patientAllergy.AllergyId equals allergy.Id
                               select new { allergy.Name, patientAllergy.Severity })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var mealCount = await _context.Meals
            .AsNoTracking()
            .CountAsync(meal => meal.PatientId == patientId && meal.ConsumedAt >= start && meal.ConsumedAt <= end, ct)
            .ConfigureAwait(false);

        var frequentFoods = await ResolveFrequentFoodsAsync(patientId, start, end, ct).ConfigureAwait(false);

        var symptomTypes = await _context.Symptoms
            .AsNoTracking()
            .Where(symptom => symptom.PatientId == patientId && symptom.OccurredAt >= start && symptom.OccurredAt <= end)
            .Select(symptom => symptom.SymptomType)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var symptoms = symptomTypes
            .GroupBy(type => type)
            .Select(group => new ReportSymptomSummary(group.Key.ToString(), group.Count()))
            .OrderByDescending(summary => summary.Count)
            .ToList();

        var assessmentRows = await _context.IbsSssAssessments
            .AsNoTracking()
            .Where(assessment => assessment.PatientId == patientId && assessment.CompletedAt >= start && assessment.CompletedAt <= end)
            .OrderBy(assessment => assessment.CompletedAt)
            .Select(assessment => new { assessment.CompletedAt, assessment.TotalScore, assessment.SeverityCategory })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var assessments = assessmentRows
            .Select(row => new ReportAssessment(DateOnly.FromDateTime(row.CompletedAt), row.TotalScore, row.SeverityCategory.ToString()))
            .ToList();

        var recommendationRows = await _context.Recommendations
            .AsNoTracking()
            .Include(recommendation => recommendation.Items)
            .Where(recommendation => recommendation.PatientId == patientId
                && recommendation.ReviewedAt != null
                && recommendation.ReviewedAt >= start
                && recommendation.ReviewedAt <= end
                && ApprovedStatuses.Contains(recommendation.Status))
            .OrderBy(recommendation => recommendation.ReviewedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var recommendations = recommendationRows
            .Select(recommendation => new ReportRecommendation(
                recommendation.ReviewedAt!.Value,
                recommendation.Items.Count,
                recommendation.AiExplanation))
            .ToList();

        var feedbackRows = await (from fb in _context.RecommendationFeedback.AsNoTracking()
                                  join recommendation in _context.Recommendations.AsNoTracking()
                                      on fb.RecommendationId equals recommendation.Id
                                  where recommendation.PatientId == patientId
                                      && fb.SubmittedAt >= start
                                      && fb.SubmittedAt <= end
                                  orderby fb.SubmittedAt
                                  select new { fb.SubmittedAt, fb.WasApplied, fb.Outcome })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var feedback = feedbackRows
            .Select(row => new ReportFeedback(row.SubmittedAt, row.WasApplied, row.Outcome.ToString()))
            .ToList();

        return new ClinicalReportData(
            BuildInitials(patientName),
            profile.GetAge(DateTime.UtcNow),
            profile.IbsSubtype.ToString(),
            profile.WeightKg,
            profile.HeightCm,
            profile.Medications,
            nutritionistName,
            periodStart,
            periodEnd,
            DateTime.UtcNow,
            allergies.Select(allergy => new ReportAllergy(allergy.Name, allergy.Severity.ToString())).ToList(),
            mealCount,
            frequentFoods,
            symptoms,
            assessments,
            recommendations,
            feedback);
    }

    private async Task<IReadOnlyList<ReportFoodFrequency>> ResolveFrequentFoodsAsync(
        Guid patientId,
        DateTime start,
        DateTime end,
        CancellationToken ct)
    {
        var mealItems = from meal in _context.Meals.AsNoTracking()
                        where meal.PatientId == patientId && meal.ConsumedAt >= start && meal.ConsumedAt <= end
                        join item in _context.MealItems.AsNoTracking() on meal.Id equals item.MealId
                        select item;

        var catalogNames = await (from item in mealItems
                                  where item.FoodId != null
                                  join food in _context.FoodItems.AsNoTracking() on item.FoodId equals food.Id
                                  select food.Name)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var customNames = await (from item in mealItems
                                 where item.CustomFoodId != null
                                 join customFood in _context.CustomFoods.AsNoTracking() on item.CustomFoodId equals customFood.Id
                                 select customFood.Name)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return catalogNames
            .Concat(customNames)
            .GroupBy(name => name)
            .Select(group => new ReportFoodFrequency(group.Key, group.Count()))
            .OrderByDescending(food => food.Count)
            .ThenBy(food => food.FoodName)
            .Take(_options.TopFrequentFoods)
            .ToList();
    }

    private static (DateTime Start, DateTime End) ToUtcRange(DateOnly periodStart, DateOnly periodEnd)
    {
        var start = DateTime.SpecifyKind(periodStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(periodEnd.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);
        return (start, end);
    }

    internal static string BuildInitials(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "—";
        }

        var initials = fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => $"{char.ToUpperInvariant(part[0])}.");

        return string.Join(' ', initials);
    }
}
