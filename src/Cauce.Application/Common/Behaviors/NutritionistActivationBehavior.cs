using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using MediatR;

namespace Cauce.Application.Common.Behaviors;

/// <summary>
/// Comportamiento del pipeline de MediatR que activa a un nutricionista pendiente la primera vez que
/// opera con un token ya emitido (acta A51). Cubre el camino que no pasa por <c>POST /auth/login</c>,
/// como el del portal web con Authorization Code + PKCE directo contra Keycloak.
/// </summary>
/// <remarks>
/// A diferencia del resto de los behaviors, <b>confirma por su cuenta</b>: las consultas no llaman a
/// <c>SaveChangesAsync</c>, así que una activación que solo quedara enrolada se descartaría. Por eso se
/// registra antes de <see cref="AuditingBehavior{TRequest,TResponse}"/>: en ese punto no hay nada más en
/// el <c>ChangeTracker</c>, y su confirmación no puede arrastrar la fila de intención de un comando cuyo
/// handler todavía puede fallar (acta A8).
/// <para>
/// La salida temprana por rol no consulta la base: se resuelve con los roles del token. Las peticiones
/// anónimas, incluido el propio login, y las que no vienen de HTTP la atraviesan sin costo.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">Tipo de la petición.</typeparam>
/// <typeparam name="TResponse">Tipo de la respuesta.</typeparam>
public sealed class NutritionistActivationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly INutritionistActivationService _activationService;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Inicializa el comportamiento con sus dependencias.
    /// </summary>
    /// <param name="currentUserService">Servicio del usuario autenticado.</param>
    /// <param name="userRepository">Repositorio de cuentas.</param>
    /// <param name="activationService">Regla compartida de activación.</param>
    /// <param name="unitOfWork">Unidad de trabajo, para confirmar la activación.</param>
    public NutritionistActivationBehavior(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        INutritionistActivationService activationService,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _activationService = activationService;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var keycloakId = _currentUserService.UserId;
        if (keycloakId is not null && _currentUserService.Roles.Contains(UserRoles.Nutritionist))
        {
            await TryActivateAsync(keycloakId.Value, cancellationToken).ConfigureAwait(false);
        }

        return await next().ConfigureAwait(false);
    }

    private async Task TryActivateAsync(Guid keycloakId, CancellationToken ct)
    {
        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), ct).ConfigureAwait(false);
        if (user is null)
        {
            // Sin cuenta local no hay nada que activar; el handler responde con su propio error.
            return;
        }

        var activated = await _activationService
            .ActivateIfPendingAsync(user, NutritionistActivationTrigger.AuthenticatedRequest, ct)
            .ConfigureAwait(false);
        if (activated)
        {
            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
