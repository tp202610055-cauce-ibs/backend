using Cauce.Application.Common.Behaviors;
using Cauce.Application.Common.Interfaces;
using Cauce.Domain.Auditing.Enums;
using FluentAssertions;
using MediatR;

namespace Cauce.Application.Tests.Common;

/// <summary>
/// Pruebas del <see cref="AuditingBehavior{TRequest,TResponse}"/>: audita los comandos marcados
/// antes de ejecutar el handler, ignora los no marcados y excluye secretos del hash.
/// </summary>
public sealed class AuditingBehaviorTests
{
    [Fact]
    public async Task Handle_AuditableCommand_LogsBeforeInvokingNext()
    {
        var auditLogger = new CapturingAuditLogger();
        var behavior = new AuditingBehavior<AuditableCommand, Unit>(auditLogger);
        var nextCalled = false;

        await behavior.Handle(
            new AuditableCommand("secreto-A"),
            () =>
            {
                // La auditoría debe registrarse antes de ejecutar el handler.
                auditLogger.Calls.Should().Be(1);
                nextCalled = true;
                return Task.FromResult(Unit.Value);
            },
            CancellationToken.None);

        nextCalled.Should().BeTrue();
        auditLogger.Calls.Should().Be(1);
        auditLogger.LastActionType.Should().Be(AuditActionType.PasswordResetConfirm);
        auditLogger.LastEntityType.Should().Be("User");
    }

    [Fact]
    public async Task Handle_NonAuditableCommand_DoesNotLog()
    {
        var auditLogger = new CapturingAuditLogger();
        var behavior = new AuditingBehavior<PlainCommand, Unit>(auditLogger);

        await behavior.Handle(new PlainCommand(), () => Task.FromResult(Unit.Value), CancellationToken.None);

        auditLogger.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_RedactedPayload_ProducesSameHashRegardlessOfSecret()
    {
        var auditLogger = new CapturingAuditLogger();
        var behavior = new AuditingBehavior<AuditableCommand, Unit>(auditLogger);

        await behavior.Handle(new AuditableCommand("secreto-A"), () => Task.FromResult(Unit.Value), CancellationToken.None);
        await behavior.Handle(new AuditableCommand("secreto-B"), () => Task.FromResult(Unit.Value), CancellationToken.None);

        auditLogger.Hashes.Should().HaveCount(2);
        auditLogger.Hashes[0].Should().Be(auditLogger.Hashes[1]);
    }

    private sealed record AuditableCommand(string Secret) : IRequest<Unit>, IAuditableCommand
    {
        public string AuditEntityType => "User";

        public AuditActionType AuditActionType => AuditActionType.PasswordResetConfirm;

        public string? AuditAdditionalContext => null;

        // Proyección sin el secreto: el hash no debe depender de él.
        public object AuditPayload => new { action = "password_reset_confirm" };
    }

    private sealed record PlainCommand : IRequest<Unit>;

    private sealed class CapturingAuditLogger : IAuditLogger
    {
        public int Calls { get; private set; }

        public List<string?> Hashes { get; } = [];

        public AuditActionType LastActionType { get; private set; }

        public string LastEntityType { get; private set; } = string.Empty;

        public Guid? LastActorUserId { get; private set; }

        public Task LogAsync(
            AuditActionType actionType,
            string entityType,
            Guid? entityId,
            string? oldValuesHash,
            string? newValuesHash,
            string? additionalContext,
            Guid? actorUserId = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastActionType = actionType;
            LastEntityType = entityType;
            LastActorUserId = actorUserId;
            Hashes.Add(newValuesHash);
            return Task.CompletedTask;
        }
    }
}
