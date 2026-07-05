using Cauce.Domain.Auditing;
using Cauce.Domain.Auditing.Enums;
using FluentAssertions;

namespace Cauce.Domain.Tests.Auditing;

/// <summary>
/// Pruebas de la entidad <see cref="AuditLog"/>: creación por factoría y verificación de integridad.
/// </summary>
public sealed class AuditLogTests
{
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Record_ValidData_CreatesLog()
    {
        var actor = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        var log = AuditLog.Record(
            actor, AuditActionType.Approve, "Recommendation", entityId,
            oldValuesHash: null, newValuesHash: "ABC123", ipAddress: "127.0.0.1", userAgent: "agent",
            additionalContext: null, occurredAtUtc: Now);

        log.ActorUserId.Should().Be(actor);
        log.ActionType.Should().Be(AuditActionType.Approve);
        log.EntityId.Should().Be(entityId);
        log.OccurredAt.Should().Be(Now);
    }

    [Fact]
    public void Record_EmptyEntityType_Throws()
    {
        var act = () => AuditLog.Record(
            null, AuditActionType.Login, "  ", null, null, null, null, null, null, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void VerifyIntegrity_MatchingHash_ReturnsTrue()
    {
        var log = AuditLog.Record(
            null, AuditActionType.Reject, "Recommendation", Guid.NewGuid(),
            null, "HASH-VALUE", null, null, null, Now);

        log.VerifyIntegrity("HASH-VALUE").Should().BeTrue();
        log.VerifyIntegrity("OTHER").Should().BeFalse();
    }

    [Fact]
    public void Record_NullActor_RepresentsSystemActor()
    {
        var log = AuditLog.Record(
            null, AuditActionType.Create, "Meal", Guid.NewGuid(),
            null, "H", null, null, null, Now);

        log.ActorUserId.Should().BeNull();
    }
}
