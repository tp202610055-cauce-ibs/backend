namespace Cauce.Application.Identity.UseCases.CreateNutritionist;

/// <summary>
/// Resultado de la creación de un nutricionista.
/// </summary>
/// <param name="UserId">Identificador del usuario creado.</param>
/// <param name="Email">Correo electrónico del usuario.</param>
/// <param name="TemporaryCredentialsEmailSent">Indica si se envió el correo con las credenciales temporales.</param>
public sealed record CreateNutritionistResult(
    Guid UserId,
    string Email,
    bool TemporaryCredentialsEmailSent);
