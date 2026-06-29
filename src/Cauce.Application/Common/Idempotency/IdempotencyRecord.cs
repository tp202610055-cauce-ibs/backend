namespace Cauce.Application.Common.Idempotency;

/// <summary>
/// Entrada persistida en el almacén de idempotencia: asocia el hash de la carga del
/// comando con su respuesta. Permite distinguir un reintento legítimo (mismo hash) de
/// un reuso indebido del <c>client_guid</c> con carga distinta (hash diferente).
/// </summary>
/// <typeparam name="TResponse">Tipo de la respuesta del comando.</typeparam>
/// <param name="RequestHash">Hash SHA-256 (hex) de la carga serializada del comando.</param>
/// <param name="Response">Respuesta producida por el comando.</param>
public sealed record IdempotencyRecord<TResponse>(string RequestHash, TResponse Response);
