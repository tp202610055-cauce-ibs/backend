using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.Exceptions;
using FluentAssertions;

namespace Cauce.Domain.Tests.Recommendations;

/// <summary>
/// Pruebas de las reglas de creación de <see cref="RecommendationItem"/>.
/// </summary>
public sealed class RecommendationItemTests
{
    [Fact]
    public void Create_WithSubstituteAndSubstituteAction_Succeeds()
    {
        var foodId = Guid.NewGuid();
        var substituteId = Guid.NewGuid();

        var item = RecommendationItem.Create(foodId, ActionType.Substitute, "razón", substituteId);

        item.ActionType.Should().Be(ActionType.Substitute);
        item.SubstituteFoodId.Should().Be(substituteId);
    }

    [Fact]
    public void Create_WithSubstituteButNotSubstituteAction_Throws()
    {
        var act = () => RecommendationItem.Create(Guid.NewGuid(), ActionType.Avoid, null, Guid.NewGuid());

        act.Should().Throw<SubstituteFoodMismatchException>();
    }

    [Fact]
    public void Create_SubstituteActionWithoutSubstitute_Throws()
    {
        var act = () => RecommendationItem.Create(Guid.NewGuid(), ActionType.Substitute, null, null);

        act.Should().Throw<SubstituteFoodMismatchException>();
    }

    [Fact]
    public void Create_WithSubstituteEqualsFoodId_Throws()
    {
        var foodId = Guid.NewGuid();

        var act = () => RecommendationItem.Create(foodId, ActionType.Substitute, null, foodId);

        act.Should().Throw<SubstituteFoodMismatchException>();
    }

    [Fact]
    public void Create_WithEmptyFoodId_Throws()
    {
        var act = () => RecommendationItem.Create(Guid.Empty, ActionType.Suggest, null, null);

        act.Should().Throw<SubstituteFoodMismatchException>();
    }

    [Fact]
    public void Create_SuggestWithoutSubstitute_Succeeds()
    {
        var item = RecommendationItem.Create(Guid.NewGuid(), ActionType.Suggest, null, null);

        item.SubstituteFoodId.Should().BeNull();
        item.ActionType.Should().Be(ActionType.Suggest);
    }
}
