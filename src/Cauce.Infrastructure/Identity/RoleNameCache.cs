using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Cauce.Application.Common.Interfaces.Identity;

namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Implementación de <see cref="IRoleNameCache"/> sobre un <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// No necesita bloqueo de inicialización: si varias peticiones concurrentes cargan el catálogo a la
/// vez, todas escriben los mismos pares y el resultado es idéntico.
/// </summary>
public sealed class RoleNameCache : IRoleNameCache
{
    private readonly ConcurrentDictionary<int, string> _namesById = new();

    /// <inheritdoc />
    public bool TryGetName(int roleId, [MaybeNullWhen(false)] out string roleName)
    {
        return _namesById.TryGetValue(roleId, out roleName);
    }

    /// <inheritdoc />
    public void Store(IReadOnlyDictionary<int, string> catalog)
    {
        foreach (var entry in catalog)
        {
            _namesById[entry.Key] = entry.Value;
        }
    }
}
