using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Patients.Exceptions;

/// <summary>
/// Se lanza cuando se intenta una operación que requiere onboarding pendiente sobre
/// un perfil cuyo onboarding ya fue completado.
/// </summary>
public sealed class OnboardingAlreadyCompletedException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar.
    /// </summary>
    public OnboardingAlreadyCompletedException()
        : base("El onboarding del paciente ya fue completado.")
    {
    }
}
