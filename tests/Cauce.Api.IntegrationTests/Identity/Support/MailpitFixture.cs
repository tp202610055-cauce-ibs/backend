using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Cauce.Api.IntegrationTests.Identity.Support;

/// <summary>
/// Fixture que levanta un contenedor Mailpit **efímero** (SMTP + API HTTP) para verificar el envío
/// real de correos de notificación (canal email de <c>SmtpEmailNotificationSender</c>). Si Docker no
/// está disponible, marca el fixture como no disponible para que las pruebas se omitan.
/// </summary>
public sealed class MailpitFixture : IAsyncLifetime
{
    private const int SmtpContainerPort = 1025;
    private const int HttpContainerPort = 8025;

    private IContainer? _container;

    /// <summary>
    /// Indica si el contenedor está disponible.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Host SMTP mapeado.
    /// </summary>
    public string SmtpHost { get; private set; } = string.Empty;

    /// <summary>
    /// Puerto SMTP mapeado.
    /// </summary>
    public int SmtpPort { get; private set; }

    /// <summary>
    /// URL de la API HTTP de mensajes de Mailpit (<c>/api/v1/messages</c>).
    /// </summary>
    public string MessagesApiUrl { get; private set; } = string.Empty;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        try
        {
            _container = new ContainerBuilder()
                .WithImage("axllent/mailpit:latest")
                .WithPortBinding(SmtpContainerPort, assignRandomHostPort: true)
                .WithPortBinding(HttpContainerPort, assignRandomHostPort: true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(HttpContainerPort))
                .Build();
            await _container.StartAsync();

            SmtpHost = _container.Hostname;
            SmtpPort = _container.GetMappedPublicPort(SmtpContainerPort);
            var httpPort = _container.GetMappedPublicPort(HttpContainerPort);
            MessagesApiUrl = $"http://{_container.Hostname}:{httpPort}/api/v1/messages";
            IsAvailable = true;
        }
        catch (Exception)
        {
            IsAvailable = false;
        }
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
