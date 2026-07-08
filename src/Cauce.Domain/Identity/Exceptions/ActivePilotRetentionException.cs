using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Identity.Exceptions;

/// <summary>
/// Se lanza cuando un paciente inscrito en el piloto clínico activo solicita eliminar su cuenta sin
/// acusar explícitamente la retención normativa de sus datos (US26 CA02). Los datos clínicos de un
/// paciente en el piloto deben conservarse mientras dure el estudio y por el plazo legal aplicable; la
/// eliminación efectiva requiere que el paciente confirme haber sido informado de esta retención.
/// </summary>
public sealed class ActivePilotRetentionException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar sobre la retención del piloto.
    /// </summary>
    public ActivePilotRetentionException()
        : base(
            "Su cuenta está inscrita en un piloto clínico activo. Por normativa, sus datos deben "
            + "conservarse durante el estudio. Para continuar con la eliminación debe confirmar que "
            + "reconoce esta retención.")
    {
    }
}
