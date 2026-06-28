using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using Cauce.Domain.Common;

namespace Cauce.Domain.ClinicalRegistry;

/// <summary>
/// Registro de una comida del paciente. Es raíz de agregado: encapsula sus ítems y se
/// persiste atómicamente con ellos. Es inmutable tras su creación (salvo el estado de
/// sincronización). El <see cref="ClientGuid"/> se genera en el dispositivo y garantiza
/// idempotencia en la sincronización offline.
/// </summary>
public sealed class Meal : Entity, IAggregateRoot
{
    private const int MinItems = 1;
    private const int MaxItems = 50;
    private const int ClockSkewToleranceMinutes = 5;

    private readonly List<MealItem> _items = new();

    /// <summary>
    /// Identificador estable generado en el dispositivo (UUID v4). Único por comida.
    /// </summary>
    public Guid ClientGuid { get; private set; }

    /// <summary>
    /// Identificador del paciente propietario de la comida.
    /// </summary>
    public Guid PatientId { get; private set; }

    /// <summary>
    /// Momento del día asociado a la comida.
    /// </summary>
    public MealTime MealTime { get; private set; }

    /// <summary>
    /// Momento en que se consumió la comida.
    /// </summary>
    public DateTime ConsumedAt { get; private set; }

    /// <summary>
    /// Estado de sincronización de la comida.
    /// </summary>
    public SyncStatus SyncStatus { get; private set; }

    /// <summary>
    /// Momento de creación en el servidor, en UTC.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Momento de creación en el dispositivo, usado para la correlación temporal con
    /// síntomas (ventana de 4 horas).
    /// </summary>
    public DateTime ClientCreatedAt { get; private set; }

    /// <summary>
    /// Ítems que componen la comida. Siempre hay al menos uno.
    /// </summary>
    public IReadOnlyCollection<MealItem> Items => _items.AsReadOnly();

    private Meal()
    {
    }

    private Meal(
        Guid id,
        Guid clientGuid,
        Guid patientId,
        MealTime mealTime,
        DateTime consumedAt,
        DateTime clientCreatedAt,
        DateTime serverUtcNow)
        : base(id)
    {
        ClientGuid = clientGuid;
        PatientId = patientId;
        MealTime = mealTime;
        ConsumedAt = consumedAt;
        ClientCreatedAt = clientCreatedAt;
        SyncStatus = SyncStatus.SyncCompleted;
        CreatedAt = serverUtcNow;
    }

    /// <summary>
    /// Registra una nueva comida con sus ítems, validando las invariantes del agregado.
    /// </summary>
    /// <param name="id">Identificador de la comida.</param>
    /// <param name="clientGuid">Identificador del dispositivo (UUID v4).</param>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="mealTime">Momento del día.</param>
    /// <param name="consumedAt">Momento de consumo.</param>
    /// <param name="clientCreatedAt">Momento de creación en el dispositivo.</param>
    /// <param name="items">Ítems de la comida (entre 1 y 50).</param>
    /// <param name="serverUtcNow">Marca de tiempo UTC del servidor.</param>
    /// <returns>La nueva comida con sus ítems.</returns>
    /// <exception cref="InvalidMealRegistrationException">Si se viola alguna invariante del registro.</exception>
    public static Meal Register(
        Guid id,
        Guid clientGuid,
        Guid patientId,
        MealTime mealTime,
        DateTime consumedAt,
        DateTime clientCreatedAt,
        IEnumerable<MealItemInput> items,
        DateTime serverUtcNow)
    {
        if (clientGuid == Guid.Empty)
        {
            throw new InvalidMealRegistrationException("El identificador de cliente (client_guid) es obligatorio.");
        }

        if (patientId == Guid.Empty)
        {
            throw new InvalidMealRegistrationException("El identificador del paciente es obligatorio.");
        }

        var tolerance = serverUtcNow.AddMinutes(ClockSkewToleranceMinutes);
        if (consumedAt > tolerance)
        {
            throw new InvalidMealRegistrationException("La fecha de consumo no puede ser futura.");
        }

        if (clientCreatedAt > tolerance)
        {
            throw new InvalidMealRegistrationException("La fecha de creación en el dispositivo no puede ser futura.");
        }

        var itemList = items as IReadOnlyList<MealItemInput> ?? items.ToList();
        if (itemList.Count is < MinItems or > MaxItems)
        {
            throw new InvalidMealRegistrationException($"Una comida debe tener entre {MinItems} y {MaxItems} ítems.");
        }

        var meal = new Meal(id, clientGuid, patientId, mealTime, consumedAt, clientCreatedAt, serverUtcNow);

        foreach (var item in itemList)
        {
            var hasFood = item.FoodId.HasValue;
            var hasCustomFood = item.CustomFoodId.HasValue;
            if (hasFood == hasCustomFood)
            {
                throw new InvalidMealRegistrationException("Cada ítem debe referenciar exactamente un alimento del catálogo o uno personalizado.");
            }

            if (item.Quantity <= 0m)
            {
                throw new InvalidMealRegistrationException("La cantidad de cada ítem debe ser mayor que cero.");
            }

            meal._items.Add(MealItem.Create(Guid.NewGuid(), meal.Id, item.FoodId, item.CustomFoodId, item.Quantity, item.Unit));
        }

        return meal;
    }

    /// <summary>
    /// Marca la comida como sincronizada. Es idempotente.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC de la operación.</param>
    public void MarkAsSynced(DateTime utcNow)
    {
        _ = utcNow;
        SyncStatus = SyncStatus.SyncCompleted;
    }

    /// <summary>
    /// Devuelve la cantidad de ítems de la comida.
    /// </summary>
    /// <returns>El número de ítems.</returns>
    public int GetItemCount()
    {
        return _items.Count;
    }
}
