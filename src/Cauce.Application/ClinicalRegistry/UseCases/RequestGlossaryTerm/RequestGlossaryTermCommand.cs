using Cauce.Application.ClinicalRegistry.Dtos;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.RequestGlossaryTerm;

/// <summary>
/// Comando del nutricionista autenticado para pedir que se agregue un término al glosario.
/// </summary>
/// <param name="Term">Término pedido.</param>
public sealed record RequestGlossaryTermCommand(string Term) : IRequest<RequestGlossaryTermResult>;

/// <summary>
/// Resultado de la solicitud.
/// </summary>
/// <param name="Request">La solicitud pendiente.</param>
/// <param name="Created">Indica si se creó ahora o ya existía una pendiente del mismo nutricionista.</param>
public sealed record RequestGlossaryTermResult(GlossaryTermRequestResult Request, bool Created);
