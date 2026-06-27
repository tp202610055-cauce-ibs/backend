using MediatR;

namespace Cauce.Application.Patients.UseCases.CompletePatientOnboarding;

/// <summary>
/// Comando para marcar el onboarding del paciente autenticado como completado. No
/// se expone como endpoint en este módulo: lo invoca el módulo de Registro Clínico
/// cuando se completa la evaluación IBS-SSS de línea base.
/// </summary>
public sealed record CompletePatientOnboardingCommand : IRequest;
