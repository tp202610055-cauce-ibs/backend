using System.Text.Json;
using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.Common.Auditing;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using Cauce.Domain.Identity;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.RequestGlossaryTerm;

/// <summary>
/// Handler de <see cref="RequestGlossaryTermCommand"/>. Rechaza un término que ya está en el glosario,
/// devuelve la solicitud pendiente si el mismo nutricionista la repite y audita las nuevas.
/// </summary>
public sealed class RequestGlossaryTermCommandHandler : IRequestHandler<RequestGlossaryTermCommand, RequestGlossaryTermResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IGlossaryRepository _glossaryRepository;
    private readonly IGlossaryTermRequestRepository _requestRepository;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public RequestGlossaryTermCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IGlossaryRepository glossaryRepository,
        IGlossaryTermRequestRepository requestRepository,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _glossaryRepository = glossaryRepository;
        _requestRepository = requestRepository;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<RequestGlossaryTermResult> Handle(RequestGlossaryTermCommand request, CancellationToken cancellationToken)
    {
        var nutritionistId = await ResolveNutritionistIdAsync(cancellationToken).ConfigureAwait(false);
        var term = request.Term.Trim();

        if (await _glossaryRepository.ExistsByTermAsync(term, cancellationToken).ConfigureAwait(false))
        {
            throw new GlossaryTermAlreadyExistsException();
        }

        var pending = await _requestRepository.FindPendingAsync(nutritionistId, term, cancellationToken).ConfigureAwait(false);
        if (pending is not null)
        {
            return new RequestGlossaryTermResult(ToResult(pending), Created: false);
        }

        var termRequest = GlossaryTermRequest.Submit(nutritionistId, term, DateTime.UtcNow);
        await _requestRepository.AddAsync(termRequest, cancellationToken).ConfigureAwait(false);

        // La tabla no tiene trigger: la solicitud y su auditoría se guardan en la misma transacción.
        await _auditLogger.LogAsync(
            AuditActionType.Create,
            nameof(GlossaryTermRequest),
            termRequest.Id,
            oldValuesHash: null,
            newValuesHash: AuditHash.Sha256Hex(termRequest.Term),
            additionalContext: JsonSerializer.Serialize(new { term = termRequest.Term, status = "pending" }),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new RequestGlossaryTermResult(ToResult(termRequest), Created: true);
    }

    private static GlossaryTermRequestResult ToResult(GlossaryTermRequest request) =>
        new(request.Id, request.Term, request.Status, request.RequestedAt);

    private async Task<Guid> ResolveNutritionistIdAsync(CancellationToken ct)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), ct).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var nutritionistRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, ct).ConfigureAwait(false);
        if (user.RoleId != nutritionistRoleId)
        {
            throw new UnauthorizedAccessException("Solo un nutricionista puede pedir términos para el glosario.");
        }

        return user.Id;
    }
}
