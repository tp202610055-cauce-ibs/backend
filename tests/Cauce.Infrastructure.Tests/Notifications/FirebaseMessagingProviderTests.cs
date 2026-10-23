using Cauce.Infrastructure.Notifications;
using Cauce.Infrastructure.Notifications.Senders;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Tests.Notifications;

/// <summary>
/// Pruebas de <see cref="FirebaseMessagingProvider"/>: una credencial ausente o inválida no lanza, se
/// registra como error y deja el cliente en <see langword="null"/>.
/// </summary>
public sealed class FirebaseMessagingProviderTests
{
    private readonly CapturingLogger<FirebaseMessagingProvider> _logger = new();

    private FirebaseMessagingProvider Create(string credentialsPath) =>
        new(Options.Create(new NotificationOptions { Fcm = new FcmOptions { UseFake = false, CredentialsPath = credentialsPath } }), _logger);

    private static string MissingPath() => Path.Combine(Path.GetTempPath(), $"no-existe-{Guid.NewGuid():N}.json");

    [Fact]
    public void Messaging_MissingCredentialsFile_ReturnsNullAndLogsError()
    {
        var provider = Create(MissingPath());

        var act = () => provider.Messaging;

        act.Should().NotThrow().Which.Should().BeNull();
        _logger.Levels.Should().ContainSingle().Which.Should().Be(LogLevel.Error);
    }

    [Fact]
    public void Messaging_InvalidCredentialsFile_ReturnsNullAndLogsError()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{ \"type\": \"service_account\" }");
            var provider = Create(path);

            var act = () => provider.Messaging;

            act.Should().NotThrow().Which.Should().BeNull();
            _logger.Levels.Should().ContainSingle().Which.Should().Be(LogLevel.Error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Messaging_ReadTwice_TriesToInitializeOnce()
    {
        var provider = Create(MissingPath());

        _ = provider.Messaging;
        _ = provider.Messaging;

        _logger.Levels.Should().ContainSingle();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Levels.Add(logLevel);
        }
    }
}
