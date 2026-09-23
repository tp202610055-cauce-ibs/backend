using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Implementación de <see cref="IPatientCodeGenerator"/> apoyada en la secuencia PostgreSQL
/// <c>patient_code_seq</c>.
///
/// <para>El correlativo lo entrega la base de datos, no la aplicación: <c>nextval</c> es atómico y no
/// bloquea, así que dos altas concurrentes obtienen números distintos sin coordinación en el código y
/// sin depender de un <c>MAX(patient_code) + 1</c>, que sí produciría colisiones bajo concurrencia. El
/// índice único de <c>users.patient_code</c> queda como red de seguridad.</para>
/// </summary>
public sealed class PatientCodeGenerator : IPatientCodeGenerator
{
    /// <summary>
    /// Nombre de la secuencia que entrega los correlativos.
    /// </summary>
    public const string SequenceName = "patient_code_seq";

    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el generador con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public PatientCodeGenerator(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<PatientCode> NextAsync(CancellationToken ct = default)
    {
        var correlative = await _context.Database
            .SqlQuery<long>($"SELECT nextval('patient_code_seq') AS \"Value\"")
            .SingleAsync(ct)
            .ConfigureAwait(false);

        return PatientCode.FromCorrelative(correlative);
    }
}
