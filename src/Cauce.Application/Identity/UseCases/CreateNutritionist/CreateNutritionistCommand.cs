using MediatR;

namespace Cauce.Application.Identity.UseCases.CreateNutritionist;

/// <summary>
/// Comando para provisionar administrativamente una cuenta de nutricionista. Ni el comando ni el
/// handler manejan contraseñas: el nutricionista define la suya con el enlace que envía Keycloak
/// (acta A52).
/// </summary>
/// <param name="Email">Correo electrónico del nutricionista.</param>
/// <param name="FullName">Nombre completo del nutricionista.</param>
public sealed record CreateNutritionistCommand(
    string Email,
    string FullName) : IRequest<CreateNutritionistResult>;
