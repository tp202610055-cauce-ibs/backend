using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity.Exceptions;
using MediatR;

namespace Cauce.Application.Identity.UseCases.GetMyConsentPdf;

/// <summary>
/// Handler que genera el comprobante en PDF del consentimiento vigente del paciente autenticado
/// (US01 CA04). Recupera el registro de consentimiento vigente y lo renderiza sin cifrar; el PDF es un
/// dato propio del paciente.
/// </summary>
public sealed class GetMyConsentPdfQueryHandler : IRequestHandler<GetMyConsentPdfQuery, ConsentPdfResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IConsentRecordRepository _consentRecordRepository;
    private readonly IConsentPdfRenderer _consentPdfRenderer;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetMyConsentPdfQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IConsentRecordRepository consentRecordRepository,
        IConsentPdfRenderer consentPdfRenderer)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _consentRecordRepository = consentRecordRepository;
        _consentPdfRenderer = consentPdfRenderer;
    }

    /// <inheritdoc />
    public async Task<ConsentPdfResult> Handle(GetMyConsentPdfQuery request, CancellationToken cancellationToken)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), cancellationToken).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var consent = await _consentRecordRepository.FindCurrentForUserAsync(user.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new ConsentRecordNotFoundException();

        var content = new ConsentPdfContent(
            user.FullName,
            user.Email,
            consent.DocumentVersion,
            consent.AcceptedAt,
            consent.ConsentTextHash,
            consent.IpAddress);

        var pdf = _consentPdfRenderer.Render(content);
        var fileName = $"consentimiento-{consent.DocumentVersion}.pdf";

        return new ConsentPdfResult(pdf, fileName);
    }
}
