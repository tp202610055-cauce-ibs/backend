using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Identity.Exceptions;

/// <summary>
/// Se lanza cuando se intenta anonimizar una cuenta que no pertenece a un paciente. La anonimización
/// del derecho al olvido (US26) solo aplica a pacientes; las cuentas de nutricionista se gestionan por
/// otras vías administrativas.
/// </summary>
public sealed class OnlyPatientsCanBeAnonymizedException : DomainException
{
    /// <summary>
    /// Identificador de la cuenta sobre la que se intentó la anonimización.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Inicializa la excepción con el identificador de la cuenta afectada.
    /// </summary>
    /// <param name="userId">Identificador de la cuenta.</param>
    public OnlyPatientsCanBeAnonymizedException(Guid userId)
        : base("Solo las cuentas de paciente pueden anonimizarse.")
    {
        UserId = userId;
    }
}
