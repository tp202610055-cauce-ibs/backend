using Cauce.Application.Recommendations.UseCases.ApproveRecommendation;
using Cauce.Application.Recommendations.UseCases.CreateManualRecommendation;
using Cauce.Application.Recommendations.UseCases.ModifyRecommendation;
using FluentAssertions;

namespace Cauce.Application.Tests.Recommendations;

/// <summary>
/// Pruebas de los mínimos de la nota clínica y de la clave de idempotencia de <c>modify</c>: 20
/// caracteres al aprobar (HU0017 CA1), 10 al modificar (HU0017 CA3) y en la manual (HU0029) (acta A69).
/// </summary>
public sealed class ReviewNoteValidatorsTests
{
    private static readonly string Note10 = new('n', 10);
    private static readonly string Note9 = new('n', 9);

    private static ModifyRecommendationCommand Modify(string note, Guid? clientGuid = null, string? title = null) =>
        new(Guid.NewGuid(), note, Items: null, title, Description: null, Steps: null, clientGuid ?? Guid.NewGuid());

    private static CreateManualRecommendationCommand Manual(string note) =>
        new(Guid.NewGuid(), "Título", "Descripción", Steps: null, note, ValidUntil: null);

    [Fact]
    public void ApproveValidator_NoteOf20Chars_Succeeds()
    {
        new ApproveRecommendationCommandValidator()
            .Validate(new ApproveRecommendationCommand(Guid.NewGuid(), new string('n', 20), Guid.NewGuid()))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void ApproveValidator_NoteOf19Chars_Fails()
    {
        new ApproveRecommendationCommandValidator()
            .Validate(new ApproveRecommendationCommand(Guid.NewGuid(), new string('n', 19), Guid.NewGuid()))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void ModifyValidator_NoteOf10Chars_Succeeds()
    {
        new ModifyRecommendationCommandValidator().Validate(Modify(Note10)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ModifyValidator_NoteOf9Chars_Fails()
    {
        new ModifyRecommendationCommandValidator().Validate(Modify(Note9)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ModifyValidator_NoteLongerThan2000_Fails()
    {
        new ModifyRecommendationCommandValidator().Validate(Modify(new string('n', 2001))).IsValid.Should().BeFalse();
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

    [Fact]
    public void ManualValidator_NoteOf10Chars_Succeeds()
    {
        new CreateManualRecommendationCommandValidator().Validate(Manual(Note10)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ManualValidator_NoteOf9Chars_Fails()
    {
        new CreateManualRecommendationCommandValidator().Validate(Manual(Note9)).IsValid.Should().BeFalse();
    }
}
