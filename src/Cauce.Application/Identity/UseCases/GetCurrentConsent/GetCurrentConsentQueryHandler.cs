using Cauce.Application.Common.Interfaces.Identity;
using MediatR;

namespace Cauce.Application.Identity.UseCases.GetCurrentConsent;

/// <summary>
/// Handler que publica el documento de consentimiento vigente. Devuelve el hash junto al texto de
/// forma deliberada: el registro compara el hash recibido contra el del documento vigente, y el
/// cálculo no aplica ninguna normalización (ni recorte, ni CRLF a LF, ni Unicode), así que una
/// diferencia de un solo byte al reproducir el texto en el cliente produciría un hash distinto y
/// un 400 <c>consent_text_mismatch</c> en todo intento de registro.
/// </summary>
public sealed class GetCurrentConsentQueryHandler
    : IRequestHandler<GetCurrentConsentQuery, CurrentConsentResult>
{
    private readonly IConsentService _consentService;

    /// <summary>
    /// Inicializa el handler con el servicio de consentimiento.
    /// </summary>
    /// <param name="consentService">Servicio que expone el documento vigente.</param>
    public GetCurrentConsentQueryHandler(IConsentService consentService)
    {
        _consentService = consentService;
    }

    /// <inheritdoc />
    public Task<CurrentConsentResult> Handle(GetCurrentConsentQuery request, CancellationToken cancellationToken)
    {
        var result = new CurrentConsentResult(
            _consentService.GetCurrentVersion(),
            _consentService.GetCurrentText(),
            _consentService.GetCurrentTextHash());

        return Task.FromResult(result);
    }
}
