using Cauce.Domain.Identity.Enums;

namespace Cauce.Application.Identity.UseCases.GetNutritionist;

/// <summary>
/// Resumen de una cuenta de nutricionista para la consulta administrativa (acta A49). Es deliberadamente
/// mínimo: identifica la cuenta y dice en qué estado de su ciclo de vida está.
/// </summary>
/// <param name="UserId">Identificador local de la cuenta.</param>
/// <param name="Email">Correo electrónico.</param>
/// <param name="FullName">Nombre completo.</param>
/// <param name="Status">Estado del ciclo de vida de la cuenta.</param>
public sealed record GetNutritionistResult(
    Guid UserId,
    string Email,
    string FullName,
    UserStatus Status);
