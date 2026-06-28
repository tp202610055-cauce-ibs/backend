using Cauce.Domain.ClinicalRegistry.Exceptions;
using Cauce.Domain.Common;

namespace Cauce.Domain.ClinicalRegistry;

/// <summary>
/// Alimento personalizado creado por un paciente a partir de ingredientes del
/// catálogo. Es raíz de agregado: encapsula su colección de ingredientes y es el
/// único punto de entrada para modificarla. La suma de proporciones de los
/// ingredientes puede diferir del tamaño de porción por mermas de cocción.
/// </summary>
public sealed class CustomFood : Entity, IAggregateRoot
{
    private const int MaxNameLength = 150;

    private readonly List<CustomFoodIngredient> _ingredients = new();

    /// <summary>
    /// Identificador del paciente propietario del alimento personalizado.
    /// </summary>
    public Guid PatientId { get; private set; }

    /// <summary>
    /// Nombre del alimento personalizado, único por paciente.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Tamaño de la porción de referencia, en gramos (mayor que cero).
    /// </summary>
    public decimal PortionSizeGrams { get; private set; }

    /// <summary>
    /// Momento de creación, en UTC.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Ingredientes que componen el alimento personalizado.
    /// </summary>
    public IReadOnlyCollection<CustomFoodIngredient> Ingredients => _ingredients.AsReadOnly();

    private CustomFood()
    {
    }

    private CustomFood(Guid id, Guid patientId, string name, decimal portionSizeGrams, DateTime utcNow)
        : base(id)
    {
        PatientId = patientId;
        Name = name;
        PortionSizeGrams = portionSizeGrams;
        CreatedAt = utcNow;
    }

    /// <summary>
    /// Crea un alimento personalizado vacío (sin ingredientes).
    /// </summary>
    /// <param name="id">Identificador del alimento personalizado.</param>
    /// <param name="patientId">Identificador del paciente propietario.</param>
    /// <param name="name">Nombre (1–150 caracteres).</param>
    /// <param name="portionSizeGrams">Tamaño de porción en gramos (mayor que cero).</param>
    /// <param name="utcNow">Marca de tiempo UTC de creación.</param>
    /// <returns>El nuevo alimento personalizado.</returns>
    /// <exception cref="ArgumentException">Si el paciente o el nombre son inválidos.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si el tamaño de porción no es mayor que cero.</exception>
    public static CustomFood Create(Guid id, Guid patientId, string name, decimal portionSizeGrams, DateTime utcNow)
    {
        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del paciente es obligatorio.", nameof(patientId));
        }

        EnsureValidName(name);

        if (portionSizeGrams <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(portionSizeGrams), "El tamaño de porción debe ser mayor que cero.");
        }

        return new CustomFood(id, patientId, name, portionSizeGrams, utcNow);
    }

    /// <summary>
    /// Agrega un ingrediente al alimento personalizado.
    /// </summary>
    /// <param name="foodId">Identificador del alimento del catálogo.</param>
    /// <param name="proportionGrams">Proporción en gramos (mayor que cero).</param>
    /// <exception cref="DuplicateIngredientException">Si el alimento ya es ingrediente.</exception>
    public void AddIngredient(Guid foodId, decimal proportionGrams)
    {
        if (_ingredients.Any(ingredient => ingredient.FoodId == foodId))
        {
            throw new DuplicateIngredientException(foodId);
        }

        _ingredients.Add(CustomFoodIngredient.Create(Guid.NewGuid(), Id, foodId, proportionGrams));
    }

    /// <summary>
    /// Quita un ingrediente del alimento personalizado.
    /// </summary>
    /// <param name="foodId">Identificador del alimento del catálogo a quitar.</param>
    /// <exception cref="IngredientNotFoundException">Si el alimento no es ingrediente.</exception>
    public void RemoveIngredient(Guid foodId)
    {
        var ingredient = _ingredients.FirstOrDefault(item => item.FoodId == foodId)
            ?? throw new IngredientNotFoundException(foodId);

        _ingredients.Remove(ingredient);
    }

    /// <summary>
    /// Actualiza el nombre del alimento personalizado.
    /// </summary>
    /// <param name="newName">Nuevo nombre (1–150 caracteres).</param>
    /// <exception cref="ArgumentException">Si el nombre es inválido.</exception>
    public void UpdateName(string newName)
    {
        EnsureValidName(newName);
        Name = newName;
    }

    /// <summary>
    /// Actualiza el tamaño de porción del alimento personalizado.
    /// </summary>
    /// <param name="newPortionSizeGrams">Nuevo tamaño de porción en gramos (mayor que cero).</param>
    /// <exception cref="ArgumentOutOfRangeException">Si el tamaño de porción no es mayor que cero.</exception>
    public void UpdatePortionSize(decimal newPortionSizeGrams)
    {
        if (newPortionSizeGrams <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(newPortionSizeGrams), "El tamaño de porción debe ser mayor que cero.");
        }

        PortionSizeGrams = newPortionSizeGrams;
    }

    /// <summary>
    /// Devuelve la diferencia entre la suma de proporciones de los ingredientes y el
    /// tamaño de porción declarado. Un valor distinto de cero es esperable por mermas
    /// de cocción; sirve solo para diagnóstico.
    /// </summary>
    /// <returns>La diferencia en gramos (positiva si los ingredientes suman más que la porción).</returns>
    public decimal GetIngredientsWeightDelta()
    {
        return _ingredients.Sum(ingredient => ingredient.ProportionGrams) - PortionSizeGrams;
    }

    private static void EnsureValidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            throw new ArgumentException("El nombre del alimento personalizado es obligatorio y no puede superar los 150 caracteres.", nameof(name));
        }
    }
}
