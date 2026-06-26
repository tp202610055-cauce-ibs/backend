namespace Cauce.Api.Contracts.Identity;

/// <summary>
/// Cuerpo de la petición de provisión de un nutricionista.
/// </summary>
/// <param name="Email">Correo electrónico del nutricionista.</param>
/// <param name="FullName">Nombre completo del nutricionista.</param>
public sealed record CreateNutritionistRequest(string Email, string FullName);
