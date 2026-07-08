using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Application.ClinicalRegistry.Dtos;

/// <summary>
/// Resultado de una consulta al glosario clínico (US27). Incluye los términos con la definición
/// apropiada al rol del solicitante y el estado del contenido.
/// </summary>
/// <param name="Terms">Términos del glosario.</param>
/// <param name="ContentStatus">Estado del contenido del glosario (borrador pendiente de validación clínica).</param>
public sealed record GlossaryResult(
    IReadOnlyList<GlossaryTermDto> Terms,
    string ContentStatus);

/// <summary>
/// Término del glosario con la definición correspondiente al rol del solicitante.
/// </summary>
/// <param name="Term">Término o sigla.</param>
/// <param name="Definition">Definición apropiada al rol (paciente o nutricionista).</param>
/// <param name="Category">Categoría del término.</param>
public sealed record GlossaryTermDto(
    string Term,
    string Definition,
    GlossaryCategory Category);
