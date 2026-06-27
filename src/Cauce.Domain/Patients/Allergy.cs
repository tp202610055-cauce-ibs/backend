using Cauce.Domain.Common;
using Cauce.Domain.Patients.Enums;

namespace Cauce.Domain.Patients;

/// <summary>
/// Entrada del catálogo de alergias e intolerancias. Es una entidad de catálogo,
/// sembrada por el sistema; los pacientes la referencian al declarar sus alergias.
/// </summary>
public sealed class Allergy : Entity, IAggregateRoot
{
    /// <summary>
    /// Nombre único de la alergia.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Naturaleza de la reacción adversa.
    /// </summary>
    public AllergyType AllergyType { get; private set; }

    /// <summary>
    /// Descripción de la alergia.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Indica si la entrada está activa en el catálogo.
    /// </summary>
    public bool IsActive { get; private set; }

    private Allergy()
    {
    }

    private Allergy(Guid id, string name, AllergyType allergyType, string? description)
        : base(id)
    {
        Name = name;
        AllergyType = allergyType;
        Description = description;
        IsActive = true;
    }

    /// <summary>
    /// Crea una entrada del catálogo. Solo debe invocarse desde el seeder del catálogo.
    /// </summary>
    /// <param name="id">Identificador de la entrada.</param>
    /// <param name="name">Nombre de la alergia.</param>
    /// <param name="type">Tipo de reacción.</param>
    /// <param name="description">Descripción.</param>
    /// <returns>La nueva entrada de catálogo.</returns>
    public static Allergy SeedEntry(Guid id, string name, AllergyType type, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Allergy(id, name, type, description);
    }

    /// <summary>
    /// Desactiva la entrada del catálogo.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Reactiva la entrada del catálogo.
    /// </summary>
    public void Reactivate()
    {
        IsActive = true;
    }
}
