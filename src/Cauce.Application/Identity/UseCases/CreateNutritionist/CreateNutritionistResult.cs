namespace Cauce.Application.Identity.UseCases.CreateNutritionist;

/// <summary>
/// Resultado de la creación de un nutricionista.
/// </summary>
/// <param name="UserId">Identificador del usuario creado.</param>
/// <param name="Email">Correo electrónico del usuario.</param>
/// <param name="ActivationEmailSent">
/// Indica si Keycloak aceptó enviar el enlace para definir la contraseña. Es <see langword="false"/>
/// cuando el envío falló: la cuenta queda provisionada igual y el enlace se vuelve a pedir con el
/// endpoint de reenvío (acta A52).
/// </param>
public sealed record CreateNutritionistResult(
    Guid UserId,
    string Email,
    bool ActivationEmailSent);
