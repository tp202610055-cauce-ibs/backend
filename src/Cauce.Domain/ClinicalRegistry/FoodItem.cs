using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Common;

namespace Cauce.Domain.ClinicalRegistry;

/// <summary>
/// Entrada del catálogo de alimentos. Es raíz de agregado y la base de las
/// recomendaciones dietéticas: su rasgo principal es el nivel de carga FODMAP. El
/// catálogo se siembra desde el seeder y, en el futuro, desde el dataset TPCA-CENAN.
/// </summary>
public sealed class FoodItem : Entity, IAggregateRoot
{
    private const int MaxNameLength = 150;
    private const int MaxCategoryLength = 50;

    /// <summary>
    /// Nombre único del alimento.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Categoría del alimento (valor controlado: cereales, proteinas, lacteos, frutas,
    /// verduras, leguminosas, preparaciones).
    /// </summary>
    public string Category { get; private set; } = string.Empty;

    /// <summary>
    /// Energía por cada 100 g, en kilocalorías.
    /// </summary>
    public decimal CaloriesPer100g { get; private set; }

    /// <summary>
    /// Proteína por cada 100 g, en gramos.
    /// </summary>
    public decimal ProteinGPer100g { get; private set; }

    /// <summary>
    /// Carbohidratos por cada 100 g, en gramos.
    /// </summary>
    public decimal CarbsGPer100g { get; private set; }

    /// <summary>
    /// Grasa por cada 100 g, en gramos.
    /// </summary>
    public decimal FatGPer100g { get; private set; }

    /// <summary>
    /// Fibra por cada 100 g, en gramos.
    /// </summary>
    public decimal FiberGPer100g { get; private set; }

    /// <summary>
    /// Nivel de carga FODMAP del alimento.
    /// </summary>
    public FodmapLevel FodmapLevel { get; private set; }

    /// <summary>
    /// Etiquetas FODMAP separadas por comas (por ejemplo, <c>fructans,polyols</c>), o
    /// <see langword="null"/> si no aplica.
    /// </summary>
    public string? FodmapTags { get; private set; }

    /// <summary>
    /// Indica si el alimento es de origen o consumo típicamente peruano.
    /// </summary>
    public bool IsPeruvian { get; private set; }

    /// <summary>
    /// Indica si el alimento está activo en el catálogo. Los alimentos referenciados no
    /// se eliminan: se desactivan.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Momento de creación de la entrada, en UTC.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Momento de la última modificación, en UTC.
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    private FoodItem()
    {
    }

    private FoodItem(
        Guid id,
        string name,
        string category,
        decimal caloriesPer100g,
        decimal proteinGPer100g,
        decimal carbsGPer100g,
        decimal fatGPer100g,
        decimal fiberGPer100g,
        FodmapLevel fodmapLevel,
        string? fodmapTags,
        bool isPeruvian,
        DateTime utcNow)
        : base(id)
    {
        Name = name;
        Category = category;
        CaloriesPer100g = caloriesPer100g;
        ProteinGPer100g = proteinGPer100g;
        CarbsGPer100g = carbsGPer100g;
        FatGPer100g = fatGPer100g;
        FiberGPer100g = fiberGPer100g;
        FodmapLevel = fodmapLevel;
        FodmapTags = fodmapTags;
        IsPeruvian = isPeruvian;
        IsActive = true;
        CreatedAt = utcNow;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Crea una entrada del catálogo de alimentos. Se invoca únicamente desde el seeder.
    /// </summary>
    /// <param name="id">Identificador de la entrada.</param>
    /// <param name="name">Nombre único del alimento.</param>
    /// <param name="category">Categoría controlada.</param>
    /// <param name="caloriesPer100g">Energía por 100 g.</param>
    /// <param name="proteinGPer100g">Proteína por 100 g.</param>
    /// <param name="carbsGPer100g">Carbohidratos por 100 g.</param>
    /// <param name="fatGPer100g">Grasa por 100 g.</param>
    /// <param name="fiberGPer100g">Fibra por 100 g.</param>
    /// <param name="fodmapLevel">Nivel de carga FODMAP.</param>
    /// <param name="fodmapTags">Etiquetas FODMAP, opcional.</param>
    /// <param name="isPeruvian">Si el alimento es peruano.</param>
    /// <param name="utcNow">Marca de tiempo UTC de creación.</param>
    /// <returns>La nueva entrada del catálogo.</returns>
    /// <exception cref="ArgumentException">Si el nombre o la categoría son inválidos.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si algún valor nutricional es negativo.</exception>
    public static FoodItem SeedEntry(
        Guid id,
        string name,
        string category,
        decimal caloriesPer100g,
        decimal proteinGPer100g,
        decimal carbsGPer100g,
        decimal fatGPer100g,
        decimal fiberGPer100g,
        FodmapLevel fodmapLevel,
        string? fodmapTags,
        bool isPeruvian,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            throw new ArgumentException("El nombre del alimento es obligatorio y no puede superar los 150 caracteres.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(category) || category.Length > MaxCategoryLength)
        {
            throw new ArgumentException("La categoría del alimento es obligatoria y no puede superar los 50 caracteres.", nameof(category));
        }

        EnsureNonNegative(caloriesPer100g, nameof(caloriesPer100g));
        EnsureNonNegative(proteinGPer100g, nameof(proteinGPer100g));
        EnsureNonNegative(carbsGPer100g, nameof(carbsGPer100g));
        EnsureNonNegative(fatGPer100g, nameof(fatGPer100g));
        EnsureNonNegative(fiberGPer100g, nameof(fiberGPer100g));

        return new FoodItem(
            id, name, category, caloriesPer100g, proteinGPer100g, carbsGPer100g,
            fatGPer100g, fiberGPer100g, fodmapLevel, fodmapTags, isPeruvian, utcNow);
    }

    /// <summary>
    /// Desactiva el alimento del catálogo. Es idempotente.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC de la operación.</param>
    public void Deactivate(DateTime utcNow)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Reactiva el alimento del catálogo. Es idempotente.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC de la operación.</param>
    public void Reactivate(DateTime utcNow)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Indica si el alimento es de carga FODMAP alta.
    /// </summary>
    /// <returns><see langword="true"/> si su nivel FODMAP es alto.</returns>
    public bool IsHighFodmap()
    {
        return FodmapLevel == FodmapLevel.High;
    }

    private static void EnsureNonNegative(decimal value, string paramName)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(paramName, "Los valores nutricionales no pueden ser negativos.");
        }
    }
}
