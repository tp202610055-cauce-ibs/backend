namespace Cauce.Domain.Common;

/// <summary>
/// Interfaz marcadora que distingue semánticamente a las entidades raíz de
/// agregado. Una raíz de agregado es el único punto de entrada para modificar
/// el agregado y la unidad de consistencia transaccional.
/// </summary>
public interface IAggregateRoot
{
}
