using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Patients.UseCases.ExportMyData;

/// <summary>
/// Handler de la exportación de portabilidad de datos del paciente autenticado (US25). Compila el ZIP
/// de CSVs, lo publica en el almacenamiento de objetos, audita la exportación con el conteo de filas por
/// entidad y devuelve la URL de descarga prefirmada.
/// </summary>
public sealed class ExportMyDataCommandHandler : IRequestHandler<ExportMyDataCommand, ExportMyDataResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IClinicalDataExporter _exporter;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExportMyDataCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public ExportMyDataCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IClinicalDataExporter exporter,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        ILogger<ExportMyDataCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _exporter = exporter;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ExportMyDataResult> Handle(ExportMyDataCommand request, CancellationToken cancellationToken)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), cancellationToken).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var patientRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Patient, cancellationToken).ConfigureAwait(false);
        if (user.RoleId != patientRoleId)
        {
            throw new UnauthorizedAccessException("Solo un paciente puede exportar sus propios datos.");
        }

        var export = await _exporter.ExportAsync(user.Id, cancellationToken).ConfigureAwait(false);

        // Auditoría explícita de la exportación con el conteo de filas por entidad (US25, Ley N° 29733).
        await _auditLogger.LogAsync(
            AuditActionType.Export,
            "PatientData",
            user.Id,
            oldValuesHash: null,
            newValuesHash: null,
            additionalContext: JsonSerializer.Serialize(new { counts = export.Counts }),
            cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Patient {UserId} exported personal data.", user.Id);

        return new ExportMyDataResult(export.DownloadUrl, export.ExpiresAtUtc);
    }
}
