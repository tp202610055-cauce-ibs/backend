namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Genera contraseñas temporales que cumplen la política del realm (mayúscula,
/// minúscula, dígito y símbolo). Se usa al provisionar nutricionistas, que deben
/// cambiar la contraseña en su primer acceso.
/// </summary>
public interface ITemporaryPasswordGenerator
{
    /// <summary>
    /// Genera una contraseña temporal aleatoria de 12 caracteres.
    /// </summary>
    /// <returns>La contraseña temporal generada.</returns>
    string Generate();
}
