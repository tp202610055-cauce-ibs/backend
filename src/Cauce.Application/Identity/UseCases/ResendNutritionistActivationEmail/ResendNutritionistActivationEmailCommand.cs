using MediatR;

namespace Cauce.Application.Identity.UseCases.ResendNutritionistActivationEmail;

/// <summary>
/// Comando para volver a pedir a Keycloak el enlace con el que un nutricionista pendiente define su
/// contraseña (acta A52). Cubre el enlace vencido, que dura 12 horas, y el correo que no llegó a salir
/// al provisionar la cuenta.
/// </summary>
/// <param name="NutritionistId">Identificador local de la cuenta del nutricionista.</param>
public sealed record ResendNutritionistActivationEmailCommand(Guid NutritionistId) : IRequest;
