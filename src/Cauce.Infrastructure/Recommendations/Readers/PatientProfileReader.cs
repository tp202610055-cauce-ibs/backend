using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Recommendations.Readers;

/// <summary>
/// Implementación de <see cref="IPatientProfileReader"/> que proyecta el perfil del paciente a
/// la instantánea de contexto. Los campos de actividad física, tabaquismo y consumo de alcohol
/// no tienen origen en el modelo actual y se completan con valores neutros por defecto (ver
/// "Deuda técnica conocida" en CLAUDE.md); solo alimentan el vector del futuro modelo ONNX.
/// </summary>
public sealed class PatientProfileReader : IPatientProfileReader
{
    private const string DefaultPhysicalActivity = "Media";

    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el lector con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public PatientProfileReader(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<PatientContextSnapshot?> GetPatientContextAsync(Guid patientId, CancellationToken ct = default)
    {
        var profile = await _context.Set<PatientProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == patientId, ct)
            .ConfigureAwait(false);

        if (profile is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var yearsSinceDiagnosis = profile.DiagnosisDate is null
            ? 0
            : Math.Max(0, now.Year - profile.DiagnosisDate.Value.Year);

        return new PatientContextSnapshot(
            profile.GetAge(now),
            MapSex(profile.BiologicalSex),
            profile.CalculateBmi(),
            MapSubtype(profile.IbsSubtype),
            yearsSinceDiagnosis,
            DefaultPhysicalActivity,
            IsSmoker: false,
            ConsumesAlcohol: false);
    }

    private static string MapSex(BiologicalSex sex)
    {
        return sex switch
        {
            BiologicalSex.Male => "Masculino",
            BiologicalSex.Female => "Femenino",
            _ => "Otro"
        };
    }

    private static string MapSubtype(IbsSubtype subtype)
    {
        return subtype switch
        {
            IbsSubtype.IbsD => "SII-D",
            IbsSubtype.IbsC => "SII-C",
            IbsSubtype.IbsM => "SII-M",
            _ => "SII-U"
        };
    }
}
