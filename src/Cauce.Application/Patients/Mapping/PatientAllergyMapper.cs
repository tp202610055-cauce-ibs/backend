using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.Dtos;
using Cauce.Domain.Patients;

namespace Cauce.Application.Patients.Mapping;

/// <summary>
/// Utilidad para mapear declaraciones de alergia a su resumen con el nombre del
/// catálogo, minimizando consultas mediante un diccionario de alergias activas.
/// </summary>
internal static class PatientAllergyMapper
{
    /// <summary>
    /// Mapea las declaraciones a sus resúmenes resolviendo el nombre del catálogo.
    /// </summary>
    /// <param name="declarations">Declaraciones de alergia del paciente.</param>
    /// <param name="allergyRepository">Repositorio del catálogo de alergias.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Resúmenes de alergia.</returns>
    public static async Task<IReadOnlyList<PatientAllergySummary>> MapAsync(
        IReadOnlyList<PatientAllergy> declarations,
        IAllergyRepository allergyRepository,
        CancellationToken ct)
    {
        if (declarations.Count == 0)
        {
            return [];
        }

        var catalog = (await allergyRepository.ListActiveAsync(ct).ConfigureAwait(false))
            .ToDictionary(a => a.Id, a => a);

        var summaries = new List<PatientAllergySummary>(declarations.Count);
        foreach (var declaration in declarations)
        {
            if (!catalog.TryGetValue(declaration.AllergyId, out var allergy))
            {
                allergy = await allergyRepository.FindByIdAsync(declaration.AllergyId, ct).ConfigureAwait(false);
            }

            summaries.Add(new PatientAllergySummary(
                declaration.Id,
                declaration.AllergyId,
                allergy?.Name ?? string.Empty,
                allergy?.AllergyType ?? default,
                declaration.Severity,
                declaration.Notes,
                declaration.DeclaredAt));
        }

        return summaries;
    }
}
