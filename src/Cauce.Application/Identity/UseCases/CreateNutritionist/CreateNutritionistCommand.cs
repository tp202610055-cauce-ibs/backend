using MediatR;

namespace Cauce.Application.Identity.UseCases.CreateNutritionist;

/// <summary>
/// Comando para provisionar administrativamente una cuenta de nutricionista. El
/// handler genera una contraseña temporal; no se recibe contraseña en el comando.
/// </summary>
/// <param name="Email">Correo electrónico del nutricionista.</param>
/// <param name="FullName">Nombre completo del nutricionista.</param>
public sealed record CreateNutritionistCommand(
    string Email,
    string FullName) : IRequest<CreateNutritionistResult>;
