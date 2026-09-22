using Cauce.Domain.Identity;

namespace Cauce.Api.IntegrationTests.Identity.Support;

/// <summary>
/// Reparte códigos de paciente únicos a las pruebas de integración que siembran usuarios saltándose
/// el flujo de alta (y, con él, la secuencia de PostgreSQL que normalmente los entrega).
///
/// <para>El contador es de proceso y monótono: dos siembras de la misma prueba, o de dos pruebas
/// distintas, nunca chocan contra el índice único <c>ux_users_patient_code</c>, que sobrevive al
/// truncado de tablas entre pruebas.</para>
/// </summary>
public static class TestPatientCodes
{
    private static int _counter;

    /// <summary>
    /// Devuelve un código de paciente no usado antes en este proceso.
    /// </summary>
    /// <returns>El código de paciente.</returns>
    public static PatientCode Next() => PatientCode.FromCorrelative(Interlocked.Increment(ref _counter));
}
