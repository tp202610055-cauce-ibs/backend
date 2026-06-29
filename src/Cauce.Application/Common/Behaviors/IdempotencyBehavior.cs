using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cauce.Application.Common.Exceptions;
using Cauce.Application.Common.Idempotency;
using Cauce.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Common.Behaviors;

/// <summary>
/// Comportamiento del pipeline de MediatR que aplica idempotencia a los comandos que
/// implementan <see cref="IIdempotentCommand"/>. Calcula el hash SHA-256 de la carga
/// del comando y lo compara con lo almacenado en el almacén de idempotencia (KeyDB):
/// un reintento con carga idéntica devuelve el resultado previo (replay); un reuso del
/// <c>client_guid</c> con carga distinta lanza <see cref="IdempotencyMismatchException"/>.
/// Si el almacén no está disponible, el comportamiento degrada (fail-open) y la
/// restricción única de <c>client_guid</c> en base de datos actúa como red de seguridad.
/// </summary>
/// <typeparam name="TRequest">Tipo de la petición.</typeparam>
/// <typeparam name="TResponse">Tipo de la respuesta.</typeparam>
public sealed class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(7);

    /// <summary>
    /// Opciones de serialización canónica del comando para el hash de idempotencia. Se
    /// usa <see cref="JsonNamingPolicy.CamelCase"/> y <c>WriteIndented = false</c> para
    /// una representación estable. Deliberadamente NO se configura
    /// <c>DefaultIgnoreCondition</c> (queda en <c>Never</c>) ni <c>IgnoreNullValues</c>:
    /// los <see langword="null"/> se serializan (<c>"campo":null</c>) y participan del
    /// hash, de modo que un campo en null se distingue de uno con valor real. Nota: el
    /// hash se calcula sobre el comando ya deserializado por MVC, por lo que un campo
    /// omitido y un null explícito en el cuerpo HTTP colapsan al mismo <see langword="null"/>
    /// de CLR y producen el mismo hash (semánticamente el mismo comando, comportamiento
    /// clínicamente correcto: dos intentos de registrar el mismo síntoma son un replay,
    /// no un conflicto). Distinguirlos exigiría hashear el cuerpo HTTP crudo vía
    /// middleware, alternativa más invasiva que no se eligió.
    /// </summary>
    private static readonly JsonSerializerOptions Canonical = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IIdempotencyStore _store;
    private readonly IIdempotencyContext _idempotencyContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<IdempotencyBehavior<TRequest, TResponse>> _logger;

    /// <summary>
    /// Inicializa el comportamiento con sus dependencias.
    /// </summary>
    /// <param name="store">Almacén de idempotencia.</param>
    /// <param name="idempotencyContext">Contexto de idempotencia de la petición.</param>
    /// <param name="currentUserService">Servicio del usuario autenticado.</param>
    /// <param name="logger">Logger de la categoría del comportamiento.</param>
    public IdempotencyBehavior(
        IIdempotencyStore store,
        IIdempotencyContext idempotencyContext,
        ICurrentUserService currentUserService,
        ILogger<IdempotencyBehavior<TRequest, TResponse>> logger)
    {
        _store = store;
        _idempotencyContext = idempotencyContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IIdempotentCommand idempotent)
        {
            return await next().ConfigureAwait(false);
        }

        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");
        var key = $"idempotency:{userId}:{idempotent.ClientGuid}";
        var requestHash = ComputeHash(request);

        var cached = await _store.GetAsync<IdempotencyRecord<TResponse>>(key, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            if (!string.Equals(cached.RequestHash, requestHash, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Idempotency mismatch for {RequestType} with client_guid {ClientGuid}",
                    typeof(TRequest).Name,
                    idempotent.ClientGuid);
                throw new IdempotencyMismatchException();
            }

            _idempotencyContext.WasReplay = true;
            _logger.LogInformation(
                "Idempotent replay for {RequestType} with client_guid {ClientGuid}",
                typeof(TRequest).Name,
                idempotent.ClientGuid);
            return cached.Response;
        }

        var response = await next().ConfigureAwait(false);
        _idempotencyContext.WasReplay = false;
        await _store
            .SetAsync(key, new IdempotencyRecord<TResponse>(requestHash, response), Ttl, cancellationToken)
            .ConfigureAwait(false);
        return response;
    }

    private static string ComputeHash(TRequest request)
    {
        var json = JsonSerializer.Serialize(request, request.GetType(), Canonical);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
    }
}
