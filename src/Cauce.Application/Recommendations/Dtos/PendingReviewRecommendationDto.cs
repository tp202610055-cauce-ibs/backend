using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Application.Recommendations.Dtos;

/// <summary>
/// Fila de la cola de revisión del nutricionista: el resumen de la recomendación más el paciente al que
/// pertenece, para que el portal no tenga que abrir cada detalle para saberlo (acta A69). No lleva el
/// código de paciente: el acta A59 lo deja fuera de las vistas de atención, donde aparecería junto al
/// nombre y anularía el seudónimo.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación.</param>
/// <param name="PatientId">Identificador de la cuenta del paciente.</param>
/// <param name="PatientFullName">Nombre completo del paciente.</param>
/// <param name="Status">Estado actual.</param>
/// <param name="ConfidenceScore">Puntaje de confianza.</param>
/// <param name="ItemsCount">Cantidad de ítems.</param>
/// <param name="GeneratedAt">Momento de generación, en UTC.</param>
/// <param name="ExpiresAt">Momento de expiración, en UTC, o <see langword="null"/>.</param>
public sealed record PendingReviewRecommendationDto(
    Guid RecommendationId,
    Guid PatientId,
    string PatientFullName,
    RecommendationStatus Status,
    decimal ConfidenceScore,
    int ItemsCount,
    DateTime GeneratedAt,
    DateTime? ExpiresAt);
