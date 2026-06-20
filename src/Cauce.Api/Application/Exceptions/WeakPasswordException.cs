namespace Cauce.Api.Application.Exceptions;

/// <summary>
/// Se lanza cuando la contraseña no cumple la política de fortaleza
/// configurada en ASP.NET Core Identity (US01 CA02).
/// </summary>
public class WeakPasswordException : Exception
{
    public IEnumerable<string> Errors { get; }

    public WeakPasswordException(IEnumerable<string> errors)
        : base("La contraseña no cumple los requisitos de seguridad.")
    {
        Errors = errors;
    }
}