namespace Cauce.Api.Commands;

/// <summary>
/// Comando de operación con argumentos propios. <see cref="CommandRunner"/> registra solo cada implementación de este
/// ensamblado.
/// </summary>
internal interface IOperationCommand
{
    /// <summary>
    /// Nombre con que se invoca: <c>dotnet Cauce.Api.dll &lt;nombre&gt;</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Ejecuta el comando. Los argumentos no incluyen el nombre.
    /// </summary>
    Task RunAsync(IServiceProvider services, IReadOnlyList<string> arguments, CancellationToken ct);
}
