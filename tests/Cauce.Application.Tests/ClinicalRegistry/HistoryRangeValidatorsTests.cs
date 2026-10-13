using Cauce.Application.ClinicalRegistry.UseCases.GetClinicalNotes;
using Cauce.Application.ClinicalRegistry.UseCases.GetMealHistory;
using Cauce.Application.ClinicalRegistry.UseCases.GetSymptomHistory;
using Cauce.Application.ClinicalRegistry.UseCases.GetUnifiedHistory;
using FluentAssertions;
using FluentValidation.Results;

namespace Cauce.Application.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas de las reglas de rango de los validadores de comidas, síntomas, notas clínicas e historial unificado:
/// las dos fechas llegan en UTC y el fin no es anterior al inicio.
/// </summary>
public sealed class HistoryRangeValidatorsTests
{
    private static readonly DateTime From = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly string[] QueryNames = ["meals", "symptoms", "clinical-notes", "history"];

    public static TheoryData<string> Queries => new(QueryNames);

    public static TheoryData<string, DateTimeKind> QueriesWithNonUtcKinds
    {
        get
        {
            var data = new TheoryData<string, DateTimeKind>();
            foreach (var query in QueryNames)
            {
                data.Add(query, DateTimeKind.Unspecified);
                data.Add(query, DateTimeKind.Local);
            }

            return data;
        }
    }

    private static ValidationResult Validate(string query, DateTime from, DateTime to) => query switch
    {
        "meals" => new GetMealHistoryQueryValidator().Validate(new GetMealHistoryQuery(from, to)),
        "symptoms" => new GetSymptomHistoryQueryValidator().Validate(new GetSymptomHistoryQuery(from, to)),
        "clinical-notes" => new GetClinicalNotesQueryValidator().Validate(new GetClinicalNotesQuery(from, to)),
        "history" => new GetUnifiedHistoryQueryValidator().Validate(new GetUnifiedHistoryQuery(from, to)),
        _ => throw new ArgumentOutOfRangeException(nameof(query), query, null)
    };

    [Theory]
    [MemberData(nameof(Queries))]
    public void Validate_UtcRange_Passes(string query)
    {
        Validate(query, From, To).IsValid.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public void Validate_SameInstant_Passes(string query)
    {
        Validate(query, From, From).IsValid.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public void Validate_ToBeforeFrom_FailsOnTo(string query)
    {
        var failure = Validate(query, To, From).Errors.Should().ContainSingle().Subject;

        failure.PropertyName.Should().Be("To");
        failure.ErrorMessage.Should().Be("La fecha final no puede ser anterior a la inicial.");
    }

    [Theory]
    [MemberData(nameof(QueriesWithNonUtcKinds))]
    public void Validate_FromNotUtc_FailsOnFrom(string query, DateTimeKind kind)
    {
        var failure = Validate(query, DateTime.SpecifyKind(From, kind), To).Errors.Should().ContainSingle().Subject;

        failure.PropertyName.Should().Be("From");
        failure.ErrorMessage.Should().Be("La fecha inicial debe incluir la zona horaria, por ejemplo Z o -05:00.");
    }

    [Theory]
    [MemberData(nameof(QueriesWithNonUtcKinds))]
    public void Validate_ToNotUtc_FailsOnTo(string query, DateTimeKind kind)
    {
        var failure = Validate(query, From, DateTime.SpecifyKind(To, kind)).Errors.Should().ContainSingle().Subject;

        failure.PropertyName.Should().Be("To");
        failure.ErrorMessage.Should().Be("La fecha final debe incluir la zona horaria, por ejemplo Z o -05:00.");
    }
}
