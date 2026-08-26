using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Interfaces.Reports;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Reports;
using Cauce.Domain.Reports.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Reports.UseCases.GenerateClinicalReport;

/// <summary>
/// Handler de la generación de reportes clínicos. Verifica la asignación nutricionista-paciente y
/// la existencia de datos, genera el PDF cifrado, persiste sus metadatos y audita la exportación de
/// forma atómica, y luego envía por correo la URL y la contraseña en mensajes separados. La
/// contraseña nunca se persiste (DEC-B5-11, acta A10).
/// </summary>
public sealed class GenerateClinicalReportCommandHandler
    : IRequestHandler<GenerateClinicalReportCommand, GenerateClinicalReportResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IClinicalReportDataReader _reportDataReader;
    private readonly IPdfReportGenerator _pdfReportGenerator;
    private readonly IClinicalReportMetadataRepository _metadataRepository;
    private readonly IEmailSender _emailSender;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GenerateClinicalReportCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GenerateClinicalReportCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        IClinicalReportDataReader reportDataReader,
        IPdfReportGenerator pdfReportGenerator,
        IClinicalReportMetadataRepository metadataRepository,
        IEmailSender emailSender,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        ILogger<GenerateClinicalReportCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
        _reportDataReader = reportDataReader;
        _pdfReportGenerator = pdfReportGenerator;
        _metadataRepository = metadataRepository;
        _emailSender = emailSender;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GenerateClinicalReportResult> Handle(
        GenerateClinicalReportCommand request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var nutritionist = await ResolveNutritionistAsync(cancellationToken).ConfigureAwait(false);

        var isAssigned = await _nutritionistPatientRepository
            .ActiveAssignmentExistsAsync(nutritionist.Id, request.PatientId, cancellationToken)
            .ConfigureAwait(false);
        if (!isAssigned)
        {
            throw new ReportAccessDeniedException();
        }

        var hasData = await _reportDataReader
            .HasDataInPeriodAsync(request.PatientId, request.PeriodStart, request.PeriodEnd, cancellationToken)
            .ConfigureAwait(false);
        if (!hasData)
        {
            throw new PatientHasNoDataInPeriodException(request.PatientId);
        }

        var report = await _pdfReportGenerator
            .GenerateAsync(request.PatientId, request.PeriodStart, request.PeriodEnd, nutritionist.Id, cancellationToken)
            .ConfigureAwait(false);

        var metadata = ClinicalReportMetadata.Register(
            request.PatientId, nutritionist.Id, request.PeriodStart, request.PeriodEnd,
            report.ObjectStoragePath, report.FileSizeBytes, now);
        await _metadataRepository.AddAsync(metadata, cancellationToken).ConfigureAwait(false);

        var context = JsonSerializer.Serialize(new
        {
            report_id = report.ReportId,
            patient_id = request.PatientId,
            range_days = request.PeriodEnd.DayNumber - request.PeriodStart.DayNumber
        });
        await _auditLogger.LogAsync(
            AuditActionType.ExportPdf, "clinical_report", report.ReportId,
            oldValuesHash: null, newValuesHash: null, additionalContext: context, cancellationToken: cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Envío síncrono y secuencial: primero la URL, luego la contraseña (orden explícito, ajuste 4).
        // La contraseña nunca se persiste (acta A10); si el envío falla, el reporte queda generado y
        // auditado y el nutricionista puede regenerarlo.
        try
        {
            await _emailSender
                .SendReportReadyAsync(nutritionist.Email, nutritionist.FullName, report.PresignedUrl, report.PresignedUrlExpiresAt, cancellationToken)
                .ConfigureAwait(false);
            await _emailSender
                .SendReportPasswordAsync(nutritionist.Email, nutritionist.FullName, report.Password, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to send report emails for report {ReportId}.", report.ReportId);
        }

        _logger.LogInformation(
            "Clinical report {ReportId} generated by nutritionist {NutritionistId} for patient {PatientId}.",
            report.ReportId, nutritionist.Id, request.PatientId);

        return new GenerateClinicalReportResult(report.ReportId, report.PresignedUrl, report.PresignedUrlExpiresAt);
    }

    private async Task<User> ResolveNutritionistAsync(CancellationToken ct)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), ct).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var nutritionistRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, ct).ConfigureAwait(false);
        if (user.RoleId != nutritionistRoleId)
        {
            throw new UnauthorizedAccessException("Solo un nutricionista puede generar reportes.");
        }

        return user;
    }
}
