namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Punto de entrada que disparó la activación de un nutricionista (acta A51). Se registra en el contexto
/// de la auditoría para poder distinguir después cuál de los dos caminos la produjo.
/// </summary>
public enum NutritionistActivationTrigger
{
    /// <summary>
    /// Inicio de sesión por <c>POST /auth/login</c>, donde la identidad recién aparece dentro del
    /// handler, una vez que Keycloak responde.
    /// </summary>
    Login,

    /// <summary>
    /// Cualquier otra petición autenticada con un token ya emitido, por ejemplo el que el portal web
    /// obtiene directamente de Keycloak sin pasar por el backend.
    /// </summary>
    AuthenticatedRequest
}
