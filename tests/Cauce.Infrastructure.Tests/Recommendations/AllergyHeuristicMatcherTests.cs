using Cauce.Infrastructure.Recommendations.Readers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cauce.Infrastructure.Tests.Recommendations;

/// <summary>
/// Pruebas del matcher heurístico de alergias (DEC-B4-14), incluyendo un caso positivo y uno
/// negativo por cada alergia del catálogo.
/// </summary>
public sealed class AllergyHeuristicMatcherTests
{
    private readonly AllergyHeuristicMatcher _matcher = new(NullLogger<AllergyHeuristicMatcher>.Instance);

    private HashSet<Guid> Forbidden(string allergyName, string foodName, string category, string? tags = null)
    {
        var foodId = Guid.NewGuid();
        var foods = new[] { new FoodCatalogEntry(foodId, foodName, category, tags) };
        return _matcher.GetForbiddenFoodIds(new[] { allergyName }, foods);
    }

    [Fact]
    public void LactoseAllergy_ExcludesMilkBasedFoods()
    {
        Forbidden("Lactosa", "Leche entera", "lacteos").Should().HaveCount(1);
    }

    [Fact]
    public void GlutenAllergy_ExcludesWheatBasedFoods()
    {
        Forbidden("Gluten", "Pan de trigo", "cereales").Should().HaveCount(1);
    }

    [Theory]
    [InlineData("Lactosa", "Yogur natural", "lacteos")]
    [InlineData("Fructosa", "Manzana verde", "frutas")]
    [InlineData("Gluten", "Fideos de trigo", "cereales")]
    [InlineData("Leguminosas", "Frijol negro", "leguminosas")]
    [InlineData("Frutos secos", "Almendras tostadas", "otros")]
    [InlineData("Mariscos", "Camarones al ajillo", "proteinas")]
    [InlineData("Pescado", "Filete de salmón", "proteinas")]
    [InlineData("Huevo", "Huevo frito", "proteinas")]
    [InlineData("Soya", "Tofu firme", "proteinas")]
    [InlineData("Sulfitos", "Vino tinto", "bebidas")]
    public void KnownAllergy_ExcludesMatchingFood(string allergy, string foodName, string category)
    {
        Forbidden(allergy, foodName, category).Should().HaveCount(1);
    }

    [Theory]
    [InlineData("Lactosa", "Arroz blanco", "cereales")]
    [InlineData("Fructosa", "Pechuga de pollo", "proteinas")]
    [InlineData("Gluten", "Naranja", "frutas")]
    [InlineData("Leguminosas", "Zanahoria", "verduras")]
    [InlineData("Frutos secos", "Leche", "lacteos")]
    [InlineData("Mariscos", "Manzana", "frutas")]
    [InlineData("Pescado", "Pan blanco", "cereales")]
    [InlineData("Huevo", "Arroz", "cereales")]
    [InlineData("Soya", "Naranja", "frutas")]
    [InlineData("Sulfitos", "Arroz integral", "cereales")]
    public void KnownAllergy_DoesNotExcludeUnrelatedFood(string allergy, string foodName, string category)
    {
        Forbidden(allergy, foodName, category).Should().BeEmpty();
    }

    [Fact]
    public void UnknownAllergy_ExcludesNothing()
    {
        Forbidden("AlergiaInexistente", "Leche entera", "lacteos").Should().BeEmpty();
    }
}
