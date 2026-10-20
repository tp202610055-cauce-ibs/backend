using Cauce.Application.Recommendations.UseCases.ApproveRecommendation;
using Cauce.Application.Recommendations.UseCases.CreateManualRecommendation;
using Cauce.Application.Recommendations.UseCases.ModifyRecommendation;
using Cauce.Application.Recommendations.UseCases.RejectRecommendation;
using FluentAssertions;
using FluentValidation.Results;

namespace Cauce.Application.Tests.Recommendations;

/// <summary>
/// Pruebas de la nota del nutricionista al aprobar, rechazar, modificar y crear: de 10 a 2000 caracteres
/// medidos sin los espacios de los extremos, con los valores de los casos de prueba.
/// </summary>
public sealed class ReviewNoteValidatorsTests
{
    private static readonly string Note10 = new('n', 10);

    public static TheoryData<string, bool> NotesFromTestCases => new()
    {
        { string.Empty, false },
        { "Muy poco.", false },
        { "Ajuste OK.", true },
        { "a" + new string(' ', 9), false },
        { "   Ajuste OK.   ", true },
        { new string('n', 2000), true },
        { new string('n', 2000) + "   ", true },
        { new string('n', 2001), false },
    };

    [Theory]
    [MemberData(nameof(NotesFromTestCases))]
    public void ApproveValidator_MeasuresTheTrimmedNote(string note, bool expectedValid)
    {
        Approve(note).IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [MemberData(nameof(NotesFromTestCases))]
    public void RejectValidator_MeasuresTheTrimmedReason(string reason, bool expectedValid)
    {
        Reject(reason).IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [MemberData(nameof(NotesFromTestCases))]
    public void ModifyValidator_MeasuresTheTrimmedNote(string note, bool expectedValid)
    {
        new ModifyRecommendationCommandValidator().Validate(Modify(note)).IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [MemberData(nameof(NotesFromTestCases))]
    public void ManualValidator_MeasuresTheTrimmedNote(string note, bool expectedValid)
    {
        new CreateManualRecommendationCommandValidator().Validate(Manual(note)).IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void ApproveValidator_EmptyNote_ReportsOnlyTheMissingNote()
    {
        var result = Approve("   ");

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(ApproveRecommendationCommand.Note))
            .Which.ErrorMessage.Should().Be("Falta la nota clínica.");
    }

    [Fact]
    public void RejectValidator_ReasonLongerThan2000_ReportsTheMaximum()
    {
        Reject(new string('n', 2001)).Errors.Should()
            .ContainSingle(error => error.ErrorMessage == "El motivo de rechazo no puede superar los 2000 caracteres.");
    }

    [Fact]
    public void ModifyValidator_EmptyIdempotencyKey_FailsOnClientGuid()
    {
        var result = new ModifyRecommendationCommandValidator().Validate(Modify(Note10, Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(ModifyRecommendationCommand.ClientGuid));
    }

    [Fact]
    public void ModifyValidator_TitleLongerThan200_Fails()
    {
        new ModifyRecommendationCommandValidator().Validate(Modify(Note10, title: new string('t', 201))).IsValid.Should().BeFalse();
    }

    private static ValidationResult Approve(string note) =>
        new ApproveRecommendationCommandValidator().Validate(new ApproveRecommendationCommand(Guid.NewGuid(), note, Guid.NewGuid()));

    private static ValidationResult Reject(string reason) =>
        new RejectRecommendationCommandValidator().Validate(new RejectRecommendationCommand(Guid.NewGuid(), reason, Guid.NewGuid()));

    private static ModifyRecommendationCommand Modify(string note, Guid? clientGuid = null, string? title = null) =>
        new(Guid.NewGuid(), note, Items: null, title, Description: null, Steps: null, clientGuid ?? Guid.NewGuid());

    private static CreateManualRecommendationCommand Manual(string note) =>
        new(Guid.NewGuid(), "Título", "Descripción", Steps: null, note, ValidUntil: null);
}
