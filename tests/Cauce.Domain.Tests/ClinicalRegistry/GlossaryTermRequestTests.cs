using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using FluentAssertions;

namespace Cauce.Domain.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas de la solicitud de un término nuevo del glosario.
/// </summary>
public sealed class GlossaryTermRequestTests
{
    private static readonly DateTime Now = new(2026, 10, 27, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Submit_ValidTerm_IsPendingWithTheTrimmedTerm()
    {
        var nutritionistId = Guid.NewGuid();

        var request = GlossaryTermRequest.Submit(nutritionistId, "  disacaridasas  ", Now);

        request.Id.Should().NotBeEmpty();
        request.NutritionistId.Should().Be(nutritionistId);
        request.Term.Should().Be("disacaridasas");
        request.Status.Should().Be(GlossaryTermRequestStatus.Pending);
        request.RequestedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Submit_BlankTerm_Throws(string term)
    {
        var act = () => GlossaryTermRequest.Submit(Guid.NewGuid(), term, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Submit_TermLongerThanTheMaximum_Throws()
    {
        var act = () => GlossaryTermRequest.Submit(Guid.NewGuid(), new string('a', GlossaryTermRequest.MaxTermLength + 1), Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Submit_EmptyNutritionist_Throws()
    {
        var act = () => GlossaryTermRequest.Submit(Guid.Empty, "disacaridasas", Now);

        act.Should().Throw<ArgumentException>();
    }
}
