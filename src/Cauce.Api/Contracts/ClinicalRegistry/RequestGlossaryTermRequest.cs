namespace Cauce.Api.Contracts.ClinicalRegistry;

/// <summary>
/// Solicitud para agregar un término al glosario.
/// </summary>
/// <param name="Term">Término pedido; se recortan los espacios de los extremos y admite hasta 100 caracteres.</param>
public sealed record RequestGlossaryTermRequest(string Term);
