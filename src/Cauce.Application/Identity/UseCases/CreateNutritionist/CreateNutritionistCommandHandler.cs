using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Identity.UseCases.CreateNutritionist;

/// <summary>
/// Handler de la provisión de nutricionistas. Crea el usuario en Keycloak con una
/// contraseña temporal que obliga al cambio en el primer acceso, persiste la
/// cuenta local y envía las credenciales por correo. Ante un fallo posterior a la
/// creación en Keycloak, compensa eliminando el usuario.
/// </summary>
public sealed class CreateNutritionistCommandHandler : IRequestHandler<CreateNutritionistCommand, CreateNutritionistResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IKeycloakAdminClient _keycloakAdminClient;
    private readonly ITemporaryPasswordGenerator _temporaryPasswordGenerator;
    private readonly IEmailSender _emailSender;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<CreateNutritionistCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public CreateNutritionistCommandHandler(
        IUserRepository userRepository,
        IKeycloakAdminClient keycloakAdminClient,
        ITemporaryPasswordGenerator temporaryPasswordGenerator,
        IEmailSender emailSender,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        ILogger<CreateNutritionistCommandHandler> logger)
    {
        _userRepository = userRepository;
        _keycloakAdminClient = keycloakAdminClient;
        _temporaryPasswordGenerator = temporaryPasswordGenerator;
        _emailSender = emailSender;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateNutritionistResult> Handle(CreateNutritionistCommand request, CancellationToken cancellationToken)
    {
        if (await _userRepository.ExistsByEmailAsync(request.Email, cancellationToken).ConfigureAwait(false))
        {
            throw new DuplicateEmailException();
        }

        var nutritionistRoleId = await _userRepository
            .GetRoleIdAsync(UserRoles.Nutritionist, cancellationToken)
            .ConfigureAwait(false);

        var temporaryPassword = _temporaryPasswordGenerator.Generate();

        var keycloakId = await _keycloakAdminClient
            .CreateUserAsync(request.Email, request.FullName, UserRoles.Nutritionist, requireEmailVerification: false, cancellationToken)
            .ConfigureAwait(false);

        User user;
        try
        {
            await _keycloakAdminClient
                .SetTemporaryPasswordAsync(keycloakId, temporaryPassword, cancellationToken)
                .ConfigureAwait(false);

            user = User.CreateNutritionist(Guid.NewGuid(), keycloakId, request.Email, request.FullName, nutritionistRoleId);
            await _userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _emailSender
                .SendNutritionistTemporaryCredentialsAsync(user.Email, user.FullName, temporaryPassword, cancellationToken)
                .ConfigureAwait(false);

            await _auditLogger.LogAsync(
                AuditActionType.Register,
                nameof(User),
                user.Id,
                oldValuesHash: null,
                newValuesHash: null,
                additionalContext: JsonSerializer.Serialize(new { actor = "admin_api_key", role = UserRoles.Nutritionist }),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Nutritionist provisioning failed after Keycloak user creation; compensating by deleting Keycloak user.");
            await CompensateKeycloakAsync(keycloakId, cancellationToken).ConfigureAwait(false);
            throw;
        }

        _logger.LogInformation("Nutritionist {UserId} provisioned successfully.", user.Id);

        return new CreateNutritionistResult(user.Id, user.Email, TemporaryCredentialsEmailSent: true);
    }

    private async Task CompensateKeycloakAsync(string keycloakId, CancellationToken ct)
    {
        try
        {
            await _keycloakAdminClient.DeleteUserAsync(keycloakId, ct).ConfigureAwait(false);
        }
        catch (Exception compensationException)
        {
            _logger.LogError(
                compensationException,
                "Compensation failed: could not delete Keycloak user after a failed provisioning.");
        }
    }
}
