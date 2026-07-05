using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.Workers;

/// <summary>
/// Worker que, cada lunes a las 09:00 hora de Lima, agenda un recordatorio push para cada paciente
/// activo, invitándolo a mantener su registro clínico al día. El servidor corre en UTC: la próxima
/// ejecución se calcula convirtiendo la hora local de Lima a UTC con <see cref="TimeZoneInfo"/>,
/// nunca con aritmética directa de husos (ajuste 5).
/// </summary>
public sealed class WeeklyRecommendationReminderWorker : BackgroundService
{
    private const string ReminderTitle = "Cauce — Recordatorio semanal";
    private const string ReminderBody =
        "No olvides registrar tus comidas y síntomas esta semana para recibir mejores recomendaciones.";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WeeklyRecommendationReminderWorker> _logger;

    /// <summary>
    /// Inicializa el worker con sus dependencias.
    /// </summary>
    /// <param name="scopeFactory">Fábrica de ámbitos de servicio.</param>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <param name="logger">Logger de la categoría del worker.</param>
    public WeeklyRecommendationReminderWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<WeeklyRecommendationReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Workers:WeeklyReminder:Enabled", true))
        {
            _logger.LogInformation("WeeklyRecommendationReminderWorker is disabled by configuration.");
            return;
        }

        var limaTimeZone = ResolveLimaTimeZone();

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = ComputeNextRunUtc(DateTime.UtcNow, limaTimeZone) - DateTime.UtcNow;
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                await ScheduleRemindersAsync(stoppingToken).ConfigureAwait(false);
                // Evita re-disparar en el mismo minuto de frontera antes de recalcular la próxima semana.
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error while scheduling weekly reminders.");
            }
        }
    }

    private async Task ScheduleRemindersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var scheduler = scope.ServiceProvider.GetRequiredService<INotificationScheduler>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var patientRoleId = await userRepository.GetRoleIdAsync(UserRoles.Patient, ct).ConfigureAwait(false);
        var patientIds = await context.Users
            .AsNoTracking()
            .Where(user => user.RoleId == patientRoleId && user.Status == UserStatus.Active)
            .Select(user => user.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        foreach (var patientId in patientIds)
        {
            var notification = Notification.Schedule(
                patientId, NotificationType.Reminder, NotificationChannel.Push, ReminderTitle, ReminderBody, now);
            await scheduler.ScheduleAsync(notification, ct).ConfigureAwait(false);
        }

        if (patientIds.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("Scheduled weekly reminders for {Count} active patient(s).", patientIds.Count);
        }
    }

    private static DateTime ComputeNextRunUtc(DateTime utcNow, TimeZoneInfo limaTimeZone)
    {
        var nowLima = TimeZoneInfo.ConvertTimeFromUtc(utcNow, limaTimeZone);
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)nowLima.DayOfWeek + 7) % 7;
        var candidate = nowLima.Date.AddDays(daysUntilMonday).AddHours(9);
        if (candidate <= nowLima)
        {
            candidate = candidate.AddDays(7);
        }

        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(candidate, DateTimeKind.Unspecified), limaTimeZone);
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
