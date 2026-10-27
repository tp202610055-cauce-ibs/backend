using Cauce.Domain.Notifications.Enums;

namespace Cauce.Application.Notifications.Dtos;

/// <summary>
/// Página de avisos tal como sale de la base, antes de redactar el mensaje.
/// </summary>
/// <param name="Rows">Avisos de la página.</param>
/// <param name="TotalCount">Total de avisos del nutricionista.</param>
/// <param name="UnreadCount">Avisos sin leer del nutricionista.</param>
public sealed record NutritionistAlertPage(IReadOnlyList<NutritionistAlertRow> Rows, int TotalCount, int UnreadCount);

/// <summary>
/// Aviso con el nombre y el código del paciente leídos al listar.
/// </summary>
/// <param name="Id">Identificador del aviso.</param>
/// <param name="Type">Tipo de aviso.</param>
/// <param name="PatientId">Identificador del paciente.</param>
/// <param name="PatientName">Nombre completo del paciente.</param>
/// <param name="PatientCode">Código del paciente, o <see langword="null"/>.</param>
/// <param name="OccurredAt">Momento del hecho, en UTC.</param>
/// <param name="ReadAt">Momento de la lectura, o <see langword="null"/>.</param>
public sealed record NutritionistAlertRow(
    Guid Id,
    NutritionistAlertType Type,
    Guid PatientId,
    string PatientName,
    string? PatientCode,
    DateTime OccurredAt,
    DateTime? ReadAt);
