namespace Cauce.Domain.Common;

/// <summary>
/// Clase base abstracta para todas las entidades de dominio. Provee identidad
/// estable mediante un <see cref="Id"/> de tipo <see cref="Guid"/> y define la
/// igualdad por referencia de identidad, no por valor de las propiedades.
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// Identificador único de la entidad. Es la base de la igualdad entre entidades.
    /// </summary>
    public Guid Id { get; protected set; }

    /// <summary>
    /// Constructor sin parámetros requerido por EF Core para materializar entidades.
    /// </summary>
    protected Entity()
    {
    }

    /// <summary>
    /// Inicializa la entidad con el identificador especificado.
    /// </summary>
    /// <param name="id">Identificador único de la entidad.</param>
    protected Entity(Guid id)
    {
        Id = id;
    }

    /// <summary>
    /// Determina si la entidad especificada es igual a la actual comparando su
    /// tipo concreto y su identificador.
    /// </summary>
    /// <param name="obj">Objeto a comparar.</param>
    /// <returns><see langword="true"/> si representan la misma entidad.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        if (Id == Guid.Empty || other.Id == Guid.Empty)
        {
            return false;
        }

        return Id == other.Id;
    }

    /// <summary>
    /// Devuelve el código hash basado en el tipo concreto y el identificador.
    /// </summary>
    /// <returns>Código hash de la entidad.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(GetType(), Id);
    }

    /// <summary>
    /// Compara dos entidades por igualdad de identidad.
    /// </summary>
    /// <param name="left">Entidad izquierda.</param>
    /// <param name="right">Entidad derecha.</param>
    /// <returns><see langword="true"/> si ambas representan la misma entidad.</returns>
    public static bool operator ==(Entity? left, Entity? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.Equals(right);
    }

    /// <summary>
    /// Compara dos entidades por desigualdad de identidad.
    /// </summary>
    /// <param name="left">Entidad izquierda.</param>
    /// <param name="right">Entidad derecha.</param>
    /// <returns><see langword="true"/> si representan entidades distintas.</returns>
    public static bool operator !=(Entity? left, Entity? right)
    {
        return !(left == right);
    }
}
