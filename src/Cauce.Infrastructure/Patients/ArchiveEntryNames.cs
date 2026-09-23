namespace Cauce.Infrastructure.Patients;

/// <summary>
/// Nombres de los archivos CSV del ZIP de portabilidad de datos del paciente (US25).
///
/// <para>Son fijos, en español y sin tildes, y <b>no dependen del idioma de la aplicación</b> (G3): el
/// ZIP puede abrirse años después, en otra máquina y por otra persona, así que el nombre de cada
/// archivo es parte del contrato del export y no una cadena de presentación. Se evitan las tildes
/// porque el nombre viaja dentro de una entrada ZIP, cuyo juego de caracteres depende del
/// descompresor.</para>
///
/// <para>Son nueve, no cinco: a los cinco clínicos (comidas, síntomas, IBS-SSS, recomendaciones y
/// perfil) se suman alergias, retroalimentación, consentimientos y auditoría, que la portabilidad de
/// la Ley N° 29733 también exige entregar.</para>
/// </summary>
public static class ArchiveEntryNames
{
    /// <summary>Perfil clínico del paciente.</summary>
    public const string Profile = "perfil_clinico.csv";

    /// <summary>Alergias declaradas.</summary>
    public const string Allergies = "alergias.csv";

    /// <summary>Comidas registradas.</summary>
    public const string Meals = "comidas.csv";

    /// <summary>Síntomas reportados.</summary>
    public const string Symptoms = "sintomas.csv";

    /// <summary>Evaluaciones IBS-SSS.</summary>
    public const string Assessments = "ibs_sss.csv";

    /// <summary>Recomendaciones dietéticas.</summary>
    public const string Recommendations = "recomendaciones.csv";

    /// <summary>Retroalimentación sobre las recomendaciones.</summary>
    public const string Feedback = "retroalimentacion.csv";

    /// <summary>Registros de consentimiento informado.</summary>
    public const string ConsentRecords = "consentimientos.csv";

    /// <summary>Entradas de auditoría en las que el paciente fue el actor.</summary>
    public const string AuditLogs = "auditoria.csv";

    /// <summary>
    /// Los nueve nombres, en el orden en que se escriben dentro del ZIP.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Profile, Allergies, Meals, Symptoms, Assessments, Recommendations, Feedback, ConsentRecords, AuditLogs
    ];
}
