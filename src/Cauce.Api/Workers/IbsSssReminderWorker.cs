using Cauce.Infrastructure.ClinicalRegistry;

namespace Cauce.Api.Workers;

/// <summary>
/// Worker que, cada día a las 09:00 hora de Lima, procesa las agendas de evaluaciones IBS-SSS (US12
/// CA02): envía recordatorios a las 48 horas de vencidas y marca como perdidas las que superan los 7
/// días. El servidor corre en UTC: la próxima ejecución se calcula convirtiendo la hora local de Lima
/// a UTC con <see cref="TimeZoneInfo"/> (ajuste 5). La lógica vive en <see cref="IbsSssScheduleProcessor"/>.
/// </summary>
public sealed class IbsSssReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IbsSssReminderWorker> _logger;

    /// <summary>
    /// Inicializa el worker con sus dependencias.
    /// </summary>
    /// <param name="scopeFactory">Fábrica de ámbitos de servicio.</param>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <param name="logger">Logger de la categoría del worker.</param>
    public IbsSssReminderWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<IbsSssReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Workers:IbsSssReminder:Enabled", true))
        {
            _logger.LogInformation("IbsSssReminderWorker is disabled by configuration.");
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
                await ProcessAsync(stoppingToken).ConfigureAwait(false);
                // Evita re-disparar en el mismo minuto de frontera antes de recalcular el próximo día.
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error while processing IBS-SSS schedules.");
            }
        }
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IbsSssScheduleProcessor>();
        await processor.ProcessAsync(DateTime.UtcNow, ct).ConfigureAwait(false);
    }

    private static DateTime ComputeNextRunUtc(DateTime utcNow, TimeZoneInfo limaTimeZone)
    {
        var nowLima = TimeZoneInfo.ConvertTimeFromUtc(utcNow, limaTimeZone);
        var candidate = nowLima.Date.AddHours(9);
        if (candidate <= nowLima)
        {
            candidate = candidate.AddDays(1);
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
