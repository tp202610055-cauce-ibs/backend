using Cauce.Domain.Notifications.Enums;

namespace Cauce.Application.Notifications.Dtos;

/// <summary>
/// Aviso de la bandeja del nutricionista.
/// </summary>
/// <param name="Id">Identificador del aviso.</param>
/// <param name="Type">Tipo de aviso.</param>
/// <param name="PatientId">Identificador del paciente.</param>
/// <param name="PatientName">Nombre completo del paciente.</param>
/// <param name="PatientCode">Código del paciente, o <see langword="null"/>.</param>
/// <param name="Message">Texto del aviso en español, listo para mostrar.</param>
/// <param name="OccurredAt">Momento del hecho, en UTC.</param>
/// <param name="IsRead">Indica si ya se leyó.</param>
/// <param name="ReadAt">Momento de la lectura, o <see langword="null"/>.</param>
public sealed record NutritionistAlertDto(
    Guid Id,
    NutritionistAlertType Type,
    Guid PatientId,
    string PatientName,
    string? PatientCode,
    string Message,
    DateTime OccurredAt,
    bool IsRead,
    DateTime? ReadAt);

/// <summary>
/// Página de la bandeja de avisos con el conteo de no leídos.
/// </summary>
/// <param name="Items">Avisos de la página, del más reciente al más antiguo.</param>
/// <param name="Page">Número de página.</param>
/// <param name="PageSize">Tamaño de página.</param>
/// <param name="TotalCount">Total de avisos.</param>
/// <param name="UnreadCount">Avisos sin leer, de todas las páginas.</param>
public sealed record NutritionistAlertListResult(
    IReadOnlyList<NutritionistAlertDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int UnreadCount);
