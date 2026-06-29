using Cauce.Application.Common.Idempotency;
using Cauce.Domain.ClinicalRegistry.Enums;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.CreateSymptom;

/// <summary>
/// Comando para registrar un síntoma del paciente autenticado. Es idempotente respecto
/// del <see cref="ClientGuid"/> generado en el dispositivo. El servidor calcula la
/// asociación con la comida más reciente dentro de la ventana de 4 horas.
/// </summary>
/// <param name="ClientGuid">Identificador estable del dispositivo (UUID v4).</param>
/// <param name="SymptomType">Tipo de síntoma.</param>
/// <param name="Intensity">Intensidad (1–100).</param>
/// <param name="OccurredAt">Momento de ocurrencia.</param>
/// <param name="ClientCreatedAt">Momento de creación en el dispositivo.</param>
public sealed record CreateSymptomCommand(
    Guid ClientGuid,
    SymptomType SymptomType,
    int Intensity,
    DateTime OccurredAt,
    DateTime ClientCreatedAt) : IRequest<CreateSymptomResult>, IIdempotentCommand;
