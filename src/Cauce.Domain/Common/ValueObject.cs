namespace Cauce.Domain.Common;

/// <summary>
/// Clase base abstracta para value objects. Un value object carece de identidad
/// propia: su igualdad se determina por el valor de sus componentes, no por una
/// referencia o un identificador.
/// </summary>
public abstract class ValueObject
{
    /// <summary>
    /// Devuelve la secuencia de componentes que determinan la igualdad del
    /// value object. Las clases derivadas deben exponer aquí cada campo relevante.
    /// </summary>
    /// <returns>Componentes que definen la igualdad estructural.</returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <summary>
    /// Determina si el value object especificado es igual al actual comparando
    /// su tipo concreto y todos sus componentes de igualdad.
    /// </summary>
    /// <param name="obj">Objeto a comparar.</param>
    /// <returns><see langword="true"/> si ambos tienen los mismos componentes.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is null || GetType() != obj.GetType())
        {
            return false;
        }

        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    /// <summary>
    /// Devuelve el código hash combinando todos los componentes de igualdad.
    /// </summary>
    /// <returns>Código hash del value object.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var component in GetEqualityComponents())
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Compara dos value objects por igualdad estructural.
    /// </summary>
    /// <param name="left">Value object izquierdo.</param>
    /// <param name="right">Value object derecho.</param>
    /// <returns><see langword="true"/> si tienen los mismos componentes.</returns>
    public static bool operator ==(ValueObject? left, ValueObject? right)
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
    /// Compara dos value objects por desigualdad estructural.
    /// </summary>
    /// <param name="left">Value object izquierdo.</param>
    /// <param name="right">Value object derecho.</param>
    /// <returns><see langword="true"/> si difieren en algún componente.</returns>
    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !(left == right);
    }
}
