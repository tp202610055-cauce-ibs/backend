using Cauce.Domain.Outbox;
using FluentAssertions;

namespace Cauce.Domain.Tests.Outbox;

/// <summary>
/// Pruebas de la entidad <see cref="OutboxMessage"/>: publicación, procesamiento, reintentos con
/// backoff exponencial y envenenamiento.
/// </summary>
public sealed class OutboxMessageTests
{
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    private static OutboxMessage Publish() =>
        OutboxMessage.Publish(Guid.NewGuid(), "recommendation", "SomeEvent", "{}", Now);

    [Fact]
    public void Publish_ValidData_CreatesUnprocessed()
    {
        var message = Publish();

        message.ProcessedAt.Should().BeNull();
        message.Attempts.Should().Be((short)0);
    }

    [Fact]
    public void Publish_EmptyAggregateType_Throws()
    {
        var act = () => OutboxMessage.Publish(Guid.NewGuid(), "  ", "SomeEvent", "{}", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsProcessed_SetsProcessedAt()
    {
        var message = Publish();

        message.MarkAsProcessed(Now);

        message.ProcessedAt.Should().Be(Now);
        message.LastError.Should().BeNull();
    }

    [Fact]
    public void RecordFailedAttempt_BelowMax_IncrementsAndDoesNotPoison()
    {
        var message = Publish();

        var poisoned = message.RecordFailedAttempt("boom", Now);

        poisoned.Should().BeFalse();
        message.Attempts.Should().Be((short)1);
        message.ProcessedAt.Should().BeNull();
        message.LastError.Should().Be("boom");
    }

    [Fact]
    public void RecordFailedAttempt_ReachingMax_PoisonsMessage()
    {
        var message = Publish();

        bool poisoned = false;
        for (var i = 0; i < 10; i++)
        {
            poisoned = message.RecordFailedAttempt("boom", Now);
        }

        poisoned.Should().BeTrue();
        message.ProcessedAt.Should().Be(Now);
    }

    [Fact]
    public void NextRetryDelay_GrowsExponentiallyAndIsCapped()
    {
        var message = Publish();

        message.RecordFailedAttempt("e", Now);
        message.NextRetryDelay().Should().Be(TimeSpan.FromSeconds(2));

        for (var i = 0; i < 20; i++)
        {
            message.RecordFailedAttempt("e", Now);
        }

        message.NextRetryDelay().Should().Be(TimeSpan.FromSeconds(3600));
    }
}
