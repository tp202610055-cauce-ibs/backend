using System.Collections.Concurrent;
using Cauce.Api.Commands;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Infrastructure.Persistence.Seeders;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Cauce.Api.IntegrationTests.Qa.Support;

/// <summary>
/// Base de las pruebas de los comandos de QA: variables de QA con contraseñas sintéticas, un manifiesto temporal por
/// prueba y el log de la ejecución capturado en memoria.
/// </summary>
public abstract class QaCommandTestBase : IntegrationTestBase, IAsyncLifetime
{
    // Valores sintéticos que cumplen la política del realm; no son contraseñas de ningún ambiente.
    protected const string PatientPassword = "Sintetica1Paciente";
    protected const string NutritionistPassword = "Sintetica2Nutri";
    protected const string SmokePassword = "Sintetica3Humo";

    private readonly List<string> _manifestFiles = [];

    protected QaCommandTestBase(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    /// <summary>
    /// Mensajes de log de los comandos ejecutados con <see cref="RunCapturingAsync"/>.
    /// </summary>
    protected ConcurrentQueue<string> CapturedLog { get; } = new();

    /// <summary>
    /// Líneas capturadas de las categorías de Cauce, para explicar un código de salida inesperado.
    /// </summary>
    protected string CauceLog => string.Join(Environment.NewLine, CapturedLog.Where(line => line.StartsWith("Cauce", StringComparison.Ordinal)));

    /// <summary>
    /// Variables de QA de la fábrica; una prueba puede quitar o cambiar alguna antes de crearla.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, string?> QaVariables => new Dictionary<string, string?>
    {
        ["QA_MODE"] = "1",
        ["QA_ALLOW_DESTRUCTIVE"] = "1",
        ["QA_PATIENT_PASSWORD"] = PatientPassword,
        ["QA_NUTRITIONIST_PASSWORD"] = NutritionistPassword,
        ["QA_SMOKE_PASSWORD"] = SmokePassword
    };

    /// <inheritdoc />
    protected override CustomWebApplicationFactory CreateFactory() =>
        new(Postgres.ConnectionString, Redis.ConnectionString, additionalSettings: QaVariables);

    /// <summary>
    /// Escribe el manifiesto en un archivo temporal y devuelve su ruta.
    /// </summary>
    protected string WriteManifest(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"qa-manifest-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        _manifestFiles.Add(path);
        return path;
    }

    /// <summary>
    /// Siembra el catálogo de consentimiento que el registro de pacientes necesita.
    /// </summary>
    protected async Task SeedConsentDocumentAsync()
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ConsentDocumentsSeeder>().SeedAsync();
    }

    /// <summary>
    /// Ejecuta un comando con la fábrica de la prueba.
    /// </summary>
    protected Task<int> RunAsync(params string[] args) => CommandRunner.RunAsync(Factory.Services, args);

    /// <summary>
    /// Ejecuta un comando con una copia de la fábrica que guarda todo su log en <see cref="CapturedLog"/>.
    /// </summary>
    protected Task<int> RunCapturingAsync(params string[] args) => RunCapturingAsync(Factory, args);

    /// <summary>
    /// Ejecuta un comando con una copia de la fábrica indicada que guarda todo su log en <see cref="CapturedLog"/>.
    /// </summary>
    protected async Task<int> RunCapturingAsync(CustomWebApplicationFactory factory, params string[] args)
    {
        var capture = new CaptureProvider(CapturedLog);
        await using var capturing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILoggerFactory>();
            services.AddSingleton<ILoggerFactory>(_ => LoggerFactory.Create(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Trace);
                logging.AddProvider(capture);
            }));
        }));

        return await CommandRunner.RunAsync(capturing.Services, args);
    }

    /// <inheritdoc />
    public new async Task DisposeAsync()
    {
        foreach (var file in _manifestFiles)
        {
            File.Delete(file);
        }

        await base.DisposeAsync();
    }

    private sealed class CaptureProvider(ConcurrentQueue<string> sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(sink, categoryName);

        public void Dispose()
        {
        }
    }

    private sealed class CaptureLogger(ConcurrentQueue<string> sink, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            sink.Enqueue($"{category}: {formatter(state, exception)}" + (exception is null ? string.Empty : " " + exception));
        }
    }
}
