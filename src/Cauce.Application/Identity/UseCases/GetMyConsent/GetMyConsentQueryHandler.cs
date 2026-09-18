using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity.Exceptions;
using MediatR;

namespace Cauce.Application.Identity.UseCases.GetMyConsent;

/// <summary>
/// Handler que publica los datos del consentimiento aceptado por el paciente autenticado.
/// </summary>
/// <remarks>
/// Existe para que la sección de privacidad del móvil pueda mostrar versión y fecha sin
/// descargar el PDF. Antes, esos dos datos solo viajaban dentro del documento binario, de modo
/// que la pantalla no podía decir qué versión había aceptado el paciente (CP004 paso 2).
/// </remarks>
public sealed class GetMyConsentQueryHandler : IRequestHandler<GetMyConsentQuery, MyConsentResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IConsentRecordRepository _consentRecordRepository;
    private readonly IConsentDocumentRepository _consentDocumentRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    /// <param name="currentUserService">Servicio del usuario autenticado.</param>
    /// <param name="userRepository">Repositorio de usuarios.</param>
    /// <param name="consentRecordRepository">Repositorio de registros de consentimiento.</param>
    /// <param name="consentDocumentRepository">Repositorio de versiones del documento.</param>
    public GetMyConsentQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IConsentRecordRepository consentRecordRepository,
        IConsentDocumentRepository consentDocumentRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _consentRecordRepository = consentRecordRepository;
        _consentDocumentRepository = consentDocumentRepository;
    }

    /// <inheritdoc />
    public async Task<MyConsentResult> Handle(GetMyConsentQuery request, CancellationToken cancellationToken)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), cancellationToken).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var consent = await _consentRecordRepository.FindCurrentForUserAsync(user.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new ConsentRecordNotFoundException();

        var document = await _consentDocumentRepository
            .FindByVersionAsync(consent.DocumentVersion, cancellationToken)
            .ConfigureAwait(false);

        return new MyConsentResult(
            consent.DocumentVersion,
            consent.AcceptedAt,
            consent.ConsentTextHash,
            document is not null);
    }
}
