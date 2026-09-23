using Cauce.Application.Patients.Dtos;
using Cauce.Domain.Patients.Enums;
using MediatR;

namespace Cauce.Application.Patients.UseCases.GetMyProfileSummary;

/// <summary>
/// Consulta del perfil agregado del paciente autenticado (US28): datos de identificación, perfil
/// clínico, fecha de inicio en el piloto, nutricionista asignado y evolución IBS-SSS resumida.
/// </summary>
public sealed record GetMyProfileSummaryQuery : IRequest<MyProfileSummaryResult>;

/// <summary>
/// Perfil agregado del paciente para su vista de "mi perfil" (US28).
/// </summary>
/// <param name="Patient">Datos de identificación del paciente (con correo enmascarado).</param>
/// <param name="Clinical">Perfil clínico resumido.</param>
/// <param name="PilotStartDate">Fecha de inicio del paciente en el piloto.</param>
/// <param name="AssignedNutritionist">Nutricionista asignado, o <see langword="null"/> si no tiene.</param>
/// <param name="IbsSssBaseline">Puntaje IBS-SSS de línea base, o <see langword="null"/>.</param>
/// <param name="IbsSssLatest">Puntaje IBS-SSS más reciente, o <see langword="null"/>.</param>
/// <param name="CumulativeChange">Cambio acumulado del puntaje (negativo indica mejoría), o <see langword="null"/>.</param>
/// <param name="SignificantClinicalResponse">Indica respuesta clínica significativa (reducción de al menos 50 puntos).</param>
public sealed record MyProfileSummaryResult(
    MyProfilePatientInfo Patient,
    MyProfileClinicalInfo Clinical,
    DateOnly PilotStartDate,
    NutritionistAssignmentSummary? AssignedNutritionist,
    int? IbsSssBaseline,
    int? IbsSssLatest,
    int? CumulativeChange,
    bool SignificantClinicalResponse);

/// <summary>
/// Datos de identificación del paciente para el perfil agregado.
/// </summary>
/// <param name="PatientCode">
/// Código correlativo legible del paciente (<c>PAC-0042</c>, acta A59). Es el identificador con el
/// que el paciente figura en el estudio, y el que la pantalla de perfil muestra (HU0028, CP070).
/// </param>
/// <param name="FullName">Nombre completo del paciente.</param>
/// <param name="MaskedEmail">Correo enmascarado (por ejemplo, <c>r***@essalud.pe</c>).</param>
public sealed record MyProfilePatientInfo(string PatientCode, string FullName, string MaskedEmail);

/// <summary>
/// Perfil clínico resumido del paciente para el perfil agregado.
/// </summary>
/// <param name="IbsSubtype">Subtipo clínico de SII.</param>
/// <param name="DiagnosisDate">Fecha de diagnóstico, si se conoce.</param>
/// <param name="Age">Edad cumplida del paciente.</param>
/// <param name="Allergies">
/// Alergias declaradas por el paciente, con la misma forma que devuelven
/// <c>GET /patients/allergies</c> y <c>GET /patients/profile</c> (HU0028, CP070). Es una lista
/// vacía, nunca <see langword="null"/>, cuando el paciente no declaró ninguna.
/// </param>
public sealed record MyProfileClinicalInfo(
    IbsSubtype IbsSubtype,
    DateOnly? DiagnosisDate,
    int Age,
    IReadOnlyList<PatientAllergySummary> Allergies);
