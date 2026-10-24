using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Application.Common.Notifications;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.ClinicalRegistry;

/// <summary>
/// Procesa las agendas IBS-SSS (US12 CA02): avisa el día del vencimiento, recuerda a las 48 horas y marca
/// como perdidas las que superan 7 días. Lo usan el worker diario y el comando <c>process-ibs-sss</c>.
/// </summary>
public sealed class IbsSssScheduleProcessor
{
    /// <summary>
    /// Tipo de entidad relacionada de las notificaciones de una agenda; la app abre el cuestionario con él.
    /// </summary>
    public const string RelatedEntityType = "ibs_sss_schedule";

    // Tipos distintos para que el aviso y el recordatorio de una agenda convivan en ux_notifications_dedup.
    private const NotificationType DueNoticeType = NotificationType.Info;
    private const NotificationType ReminderType = NotificationType.Reminder;

    private const int ReminderOverdueHours = 48;
    private const int MissedOverdueDays = 7;

    private static readonly TimeZoneInfo LimaTimeZone = ResolveLimaTimeZone();

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
    /// Procesa las agendas abiertas respecto del instante indicado.
    /// </summary>
    /// <param name="utcNow">Instante de referencia, en UTC.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Cuántos recordatorios, avisos del día y agendas perdidas se procesaron.</returns>
    public async Task<IbsSssScheduleProcessingResult> ProcessAsync(DateTime utcNow, CancellationToken ct = default)
    {
        var reminderThreshold = utcNow.AddHours(-ReminderOverdueHours);
        var missedThreshold = utcNow.AddDays(-MissedOverdueDays);
        var endOfLimaDay = EndOfLimaDayUtc(utcNow);

        // Aviso del día: vence hoy o antes en fecha de Lima, una sola vez por agenda. Si ya toca el
        // recordatorio de 48 horas, solo se envía ese.
        var dueNoticeCandidates = await _context.IbsSssSchedules
            .Where(schedule => !schedule.Completed
                && !schedule.Missed
                && schedule.ReminderSentAt == null
                && schedule.DueDate < endOfLimaDay
                && schedule.DueDate > reminderThreshold
                && !_context.Notifications.Any(notification => notification.Type == DueNoticeType
                    && notification.RelatedEntityType == RelatedEntityType
                    && notification.RelatedEntityId == schedule.Id))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var schedule in dueNoticeCandidates)
        {
            var notification = Notification.Schedule(
                schedule.PatientId, DueNoticeType, NotificationChannel.Push, PatientPushTexts.Title,
                PatientPushTexts.FollowUpAvailableBody, utcNow, RelatedEntityType, schedule.Id);
            await _notificationScheduler.ScheduleAsync(notification, ct).ConfigureAwait(false);
        }

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
                schedule.PatientId, ReminderType, NotificationChannel.Push, PatientPushTexts.Title,
                PatientPushTexts.FollowUpPendingBody, utcNow, RelatedEntityType, schedule.Id);
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

        var result = new IbsSssScheduleProcessingResult(
            reminderCandidates.Count, dueNoticeCandidates.Count, missedCandidates.Count);

        if (result.RemindersSent > 0 || result.DueNoticesSent > 0 || result.MarkedMissed > 0)
        {
            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation(
                "IBS-SSS schedule processing: {DueNotices} due notice(s), {Reminders} reminder(s) sent, {Missed} marked missed.",
                result.DueNoticesSent, result.RemindersSent, result.MarkedMissed);
        }

        return result;
    }

    /// <summary>
    /// Primer instante, en UTC, del día siguiente en Lima: lo que vence antes de él vence hoy o antes.
    /// </summary>
    /// <param name="utcNow">Instante de referencia, en UTC.</param>
    internal static DateTime EndOfLimaDayUtc(DateTime utcNow)
    {
        var limaNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, LimaTimeZone);
        var nextLimaMidnight = DateTime.SpecifyKind(limaNow.Date.AddDays(1), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(nextLimaMidnight, LimaTimeZone);
    }

    private static TimeZoneInfo ResolveLimaTimeZone()
    {
        foreach (var id in new[] { "America/Lima", "SA Pacific Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Se intenta el siguiente identificador.
            }
            catch (InvalidTimeZoneException)
            {
                // Se intenta el siguiente identificador.
            }
        }

        // Respaldo: Perú es UTC-5 todo el año (sin horario de verano).
        return TimeZoneInfo.CreateCustomTimeZone("Lima-Fallback", TimeSpan.FromHours(-5), "Lima (UTC-5)", "Lima");
    }
}
