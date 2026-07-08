using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.ClinicalRegistry;

/// <summary>
/// Procesa las agendas de evaluaciones IBS-SSS (US12 CA02): envía un recordatorio push a los pacientes
/// cuya evaluación lleva 48 horas vencida sin completarse, y marca como perdidas las que superan los 7
/// días. Se invoca desde el worker diario y directamente desde las pruebas de integración con un reloj
/// controlado (acta A12).
/// </summary>
public sealed class IbsSssScheduleProcessor
{
    private const int ReminderOverdueHours = 48;
    private const int MissedOverdueDays = 7;

    private const string ReminderTitle = "Cauce — Evaluación IBS-SSS pendiente";
    private const string ReminderBody =
        "Tu evaluación de síntomas está pendiente. Complétala para seguir tu evolución clínica.";

    private readonly CauceDbContext _context;
    private readonly INotificationScheduler _notificationScheduler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<IbsSssScheduleProcessor> _logger;

    /// <summary>
    /// Inicializa el procesador con sus dependencias.
    /// </summary>
    public IbsSssScheduleProcessor(
        CauceDbContext context,
        INotificationScheduler notificationScheduler,
        IUnitOfWork unitOfWork,
        ILogger<IbsSssScheduleProcessor> logger)
    {
        _context = context;
        _notificationScheduler = notificationScheduler;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Procesa las agendas vencidas respecto del instante indicado.
    /// </summary>
    /// <param name="utcNow">Instante de referencia, en UTC.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La cantidad de recordatorios enviados y de agendas marcadas como perdidas.</returns>
    public async Task<(int RemindersSent, int MarkedMissed)> ProcessAsync(DateTime utcNow, CancellationToken ct = default)
    {
        var reminderThreshold = utcNow.AddHours(-ReminderOverdueHours);
        var missedThreshold = utcNow.AddDays(-MissedOverdueDays);

        // Recordatorio: vencidas entre 48 horas y 7 días, sin recordatorio previo.
        var reminderCandidates = await _context.IbsSssSchedules
            .Where(schedule => !schedule.Completed
                && !schedule.Missed
                && schedule.ReminderSentAt == null
                && schedule.DueDate <= reminderThreshold
                && schedule.DueDate > missedThreshold)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var schedule in reminderCandidates)
        {
            var notification = Notification.Schedule(
                schedule.PatientId, NotificationType.Reminder, NotificationChannel.Push, ReminderTitle, ReminderBody, utcNow);
            await _notificationScheduler.ScheduleAsync(notification, ct).ConfigureAwait(false);
            schedule.MarkReminderSent(utcNow);
        }

        // No registro: vencidas por más de 7 días.
        var missedCandidates = await _context.IbsSssSchedules
            .Where(schedule => !schedule.Completed && !schedule.Missed && schedule.DueDate <= missedThreshold)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var schedule in missedCandidates)
        {
            schedule.MarkMissed();
        }

        if (reminderCandidates.Count > 0 || missedCandidates.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation(
                "IBS-SSS schedule processing: {Reminders} reminder(s) sent, {Missed} marked missed.",
                reminderCandidates.Count, missedCandidates.Count);
        }

        return (reminderCandidates.Count, missedCandidates.Count);
    }
}
