namespace Cauce.Domain.Common;

/// <summary>
/// Representa el resultado de una operación que puede producir un valor o fallar
/// con un mensaje de error. Se usa para flujos donde el fallo es esperado y forma
/// parte del contrato, en lugar de lanzar excepciones.
/// </summary>
/// <typeparam name="T">Tipo del valor producido en caso de éxito.</typeparam>
public sealed class Result<T>
{
    /// <summary>
    /// Indica si la operación fue exitosa.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Valor producido por la operación. Es <see langword="null"/> cuando la
    /// operación falló.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Mensaje de error. Es <see langword="null"/> cuando la operación fue exitosa.
    /// </summary>
    public string? Error { get; }

    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    /// <summary>
    /// Crea un resultado exitoso que envuelve el valor especificado.
    /// </summary>
    /// <param name="value">Valor producido por la operación.</param>
    /// <returns>Resultado exitoso.</returns>
    public static Result<T> Success(T value) => new(true, value, null);

    /// <summary>
    /// Crea un resultado fallido con el mensaje de error especificado.
    /// </summary>
    /// <param name="error">Descripción del fallo.</param>
    /// <returns>Resultado fallido.</returns>
    public static Result<T> Failure(string error) => new(false, default, error);
}

/// <summary>
/// Representa el resultado de una operación sin valor de retorno que puede fallar
/// con un mensaje de error. Variante no genérica de <see cref="Result{T}"/>.
/// </summary>
public sealed class Result
{
    /// <summary>
    /// Indica si la operación fue exitosa.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Mensaje de error. Es <see langword="null"/> cuando la operación fue exitosa.
    /// </summary>
    public string? Error { get; }

    private Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Crea un resultado exitoso.
    /// </summary>
    /// <returns>Resultado exitoso.</returns>
    public static Result Success() => new(true, null);

    /// <summary>
    /// Crea un resultado fallido con el mensaje de error especificado.
    /// </summary>
    /// <param name="error">Descripción del fallo.</param>
    /// <returns>Resultado fallido.</returns>
    public static Result Failure(string error) => new(false, error);
}
