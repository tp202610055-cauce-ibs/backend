using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Interfaces.Reports;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Reports.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Reports.UseCases.GenerateMyClinicalReport;

/// <summary>
/// Handler del autoreporte clínico del paciente (US24). Reutiliza la infraestructura de reportes
/// (PDF cifrado + almacenamiento + URL prefirmada) del flujo del nutricionista, resuelve el
/// nutricionista asignado (o ninguno) y envía la URL y la contraseña al propio paciente en dos
/// correos separados. La contraseña nunca se persiste (DEC-B5-11, acta A10).
/// </summary>
public sealed class GenerateMyClinicalReportCommandHandler
    : IRequestHandler<GenerateMyClinicalReportCommand, GenerateMyClinicalReportResult>
{
    private const int ReportWindowDays = 90;

    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IClinicalReportDataReader _reportDataReader;
    private readonly IPdfReportGenerator _pdfReportGenerator;
    private readonly IEmailSender _emailSender;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GenerateMyClinicalReportCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GenerateMyClinicalReportCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        IClinicalReportDataReader reportDataReader,
        IPdfReportGenerator pdfReportGenerator,
        IEmailSender emailSender,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        ILogger<GenerateMyClinicalReportCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
        _reportDataReader = reportDataReader;
        _pdfReportGenerator = pdfReportGenerator;
        _emailSender = emailSender;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GenerateMyClinicalReportResult> Handle(
        GenerateMyClinicalReportCommand request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var patient = await ResolvePatientAsync(cancellationToken).ConfigureAwait(false);

        var periodEnd = DateOnly.FromDateTime(now);
        var periodStart = periodEnd.AddDays(-ReportWindowDays);

        var hasData = await _reportDataReader
            .HasDataInPeriodAsync(patient.Id, periodStart, periodEnd, cancellationToken)
            .ConfigureAwait(false);
        if (!hasData)
        {
            throw new PatientHasNoDataInPeriodException(patient.Id);
        }

        // El nutricionista asignado, si existe, se incluye en el reporte; si no, la sección se omite.
        var assignment = await _nutritionistPatientRepository
            .FindActiveByPatientAsync(patient.Id, cancellationToken)
            .ConfigureAwait(false);

        var report = await _pdfReportGenerator
            .GenerateAsync(patient.Id, periodStart, periodEnd, assignment?.NutritionistId, cancellationToken)
            .ConfigureAwait(false);

        var context = JsonSerializer.Serialize(new
        {
            report_id = report.ReportId,
            self_service = true,
            range_days = periodEnd.DayNumber - periodStart.DayNumber
        });
        await _auditLogger.LogAsync(
            AuditActionType.ExportPdf, "clinical_report", report.ReportId,
            oldValuesHash: null, newValuesHash: null, additionalContext: context, cancellationToken: cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Envío síncrono y secuencial al propio paciente: primero la URL, luego la contraseña. La
        // contraseña nunca se persiste; si el envío falla, el reporte queda generado y auditado.
        try
        {
            await _emailSender
                .SendReportReadyAsync(patient.Email, patient.FullName, report.PresignedUrl, report.PresignedUrlExpiresAt, cancellationToken)
                .ConfigureAwait(false);
            await _emailSender
                .SendReportPasswordAsync(patient.Email, patient.FullName, report.Password, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to send self-service report emails for report {ReportId}.", report.ReportId);
        }

        _logger.LogInformation("Patient self-service clinical report {ReportId} generated.", report.ReportId);

        return new GenerateMyClinicalReportResult(report.ReportId, report.PresignedUrl, report.PresignedUrlExpiresAt);
    }

    private async Task<User> ResolvePatientAsync(CancellationToken ct)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), ct).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var patientRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Patient, ct).ConfigureAwait(false);
        if (user.RoleId != patientRoleId)
        {
            throw new UnauthorizedAccessException("Solo un paciente puede generar su autoreporte clínico.");
        }

        return user;
    }
}
