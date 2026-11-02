using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Application.ClinicalRegistry.Dtos;

/// <summary>
/// Solicitud de un término nuevo del glosario.
/// </summary>
/// <param name="Id">Identificador de la solicitud.</param>
/// <param name="Term">Término pedido.</param>
/// <param name="Status">Estado de la solicitud.</param>
/// <param name="RequestedAt">Momento de la solicitud, en UTC.</param>
public sealed record GlossaryTermRequestResult(Guid Id, string Term, GlossaryTermRequestStatus Status, DateTime RequestedAt);
