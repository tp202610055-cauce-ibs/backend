using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Identity.UseCases.ResendVerificationEmail;

/// <summary>
/// Handler del reenvío del correo de verificación (acta A40). Delega el envío a Keycloak, que es quien
/// emite y procesa el enlace de confirmación.
/// </summary>
/// <remarks>
/// El handler no distingue sus desenlaces hacia afuera: cuenta inexistente, cuenta ya verificada y
/// reenvío efectivo terminan los tres en 200 sin cuerpo (decisión D4). La bitácora es el único lugar
/// donde queda registro del intento, y la escribe el <c>AuditingBehavior</c> antes de llegar aquí; por
/// eso este handler confirma la transacción aunque no tenga cambios de negocio propios que persistir.
/// </remarks>
public sealed class ResendVerificationEmailCommandHandler : IRequestHandler<ResendVerificationEmailCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IKeycloakAdminClient _keycloakAdminClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ResendVerificationEmailCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    /// <param name="userRepository">Repositorio de usuarios.</param>
    /// <param name="keycloakAdminClient">Cliente de la Admin API de Keycloak.</param>
    /// <param name="unitOfWork">Unidad de trabajo que confirma la bitácora.</param>
    /// <param name="logger">Logger de la categoría del handler.</param>
    public ResendVerificationEmailCommandHandler(
        IUserRepository userRepository,
        IKeycloakAdminClient keycloakAdminClient,
        IUnitOfWork unitOfWork,
        ILogger<ResendVerificationEmailCommandHandler> logger)
    {
        _userRepository = userRepository;
        _keycloakAdminClient = keycloakAdminClient;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Handle(ResendVerificationEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository
            .FindByEmailAsync(request.Email, cancellationToken)
            .ConfigureAwait(false);

        if (user is not null && !user.EmailVerified)
        {
            await TrySendVerifyEmailAsync(user.KeycloakId, user.Id, cancellationToken).ConfigureAwait(false);
        }

        // Persiste el registro de auditoría que el AuditingBehavior enroló en el ChangeTracker.
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Solicita a Keycloak el reenvío del correo de verificación. Es best-effort: un fallo del proveedor
    /// no cambia la respuesta al cliente, que es uniforme por diseño.
    /// </summary>
    /// <param name="keycloakId">Identificador del usuario en Keycloak.</param>
    /// <param name="userId">Identificador local del usuario, para la traza.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    private async Task TrySendVerifyEmailAsync(string keycloakId, Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            await _keycloakAdminClient.SendVerifyEmailAsync(keycloakId, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Verification email resend requested for user {UserId}.", userId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Could not request the verification email resend for user {UserId}.",
                userId);
        }
    }
}
