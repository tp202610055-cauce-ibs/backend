using System.Globalization;
using Cauce.Domain.Patients.Enums;

namespace Cauce.Infrastructure.Qa;

/// <summary>
/// Valores fijos para lo que el manifiesto no trae. Son deterministas: dos siembras dan el mismo resultado.
/// </summary>
public static class QaDefaults
{
    /// <summary>
    /// Fecha de nacimiento de todo paciente sembrado.
    /// </summary>
    public static readonly DateOnly DateOfBirth = new(1990, 1, 1);

    /// <summary>
    /// Sexo biológico de todo paciente sembrado.
    /// </summary>
    public const BiologicalSex Sex = BiologicalSex.Female;

    /// <summary>
    /// Peso del perfil cuando el caso no lo da.
    /// </summary>
    public const decimal WeightKg = 62m;

    /// <summary>
    /// Talla del perfil cuando el caso no la da.
    /// </summary>
    public const decimal HeightCm = 165m;

    /// <summary>
    /// Subtipo del perfil cuando el caso no lo da.
    /// </summary>
    public const IbsSubtype Subtype = IbsSubtype.IbsD;

    /// <summary>
    /// Nombre visible a partir del correo cuando el manifiesto no trae uno: <c>paciente.perfil1</c> da «Paciente Perfil1».
    /// </summary>
    public static string DisplayNameFor(string email, string? displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        var localPart = email.Split('@')[0];
        var words = localPart.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpper(word[0], CultureInfo.InvariantCulture) + word[1..]);
        return string.Join(' ', words);
    }
}
