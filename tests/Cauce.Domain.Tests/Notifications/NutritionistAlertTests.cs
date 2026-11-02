using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using FluentAssertions;

namespace Cauce.Domain.Tests.Notifications;

/// <summary>
/// Pruebas del aviso de la bandeja del nutricionista.
/// </summary>
public sealed class NutritionistAlertTests
{
    private static readonly DateTime OccurredAt = new(2026, 10, 20, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Record_ValidData_StartsUnread()
    {
        var nutritionistId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        var alert = NutritionistAlert.Record(nutritionistId, patientId, NutritionistAlertType.PatientLinked, OccurredAt);

        alert.Id.Should().NotBeEmpty();
        alert.NutritionistId.Should().Be(nutritionistId);
        alert.PatientId.Should().Be(patientId);
        alert.Type.Should().Be(NutritionistAlertType.PatientLinked);
        alert.OccurredAt.Should().Be(OccurredAt);
        alert.IsRead.Should().BeFalse();
        alert.ReadAt.Should().BeNull();
    }

    [Fact]
    public void Record_EmptyNutritionist_Throws()
    {
        var act = () => NutritionistAlert.Record(Guid.Empty, Guid.NewGuid(), NutritionistAlertType.PatientLinked, OccurredAt);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Record_EmptyPatient_Throws()
    {
        var act = () => NutritionistAlert.Record(Guid.NewGuid(), Guid.Empty, NutritionistAlertType.PatientLinked, OccurredAt);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Record_UndefinedType_Throws()
    {
        var act = () => NutritionistAlert.Record(Guid.NewGuid(), Guid.NewGuid(), (NutritionistAlertType)99, OccurredAt);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MarkAsRead_SetsReadAtOnlyTheFirstTime()
    {
        var alert = NutritionistAlert.Record(Guid.NewGuid(), Guid.NewGuid(), NutritionistAlertType.PatientSubtypeChanged, OccurredAt);
        var first = OccurredAt.AddHours(1);

        alert.MarkAsRead(first);
        alert.MarkAsRead(first.AddHours(1));

        alert.IsRead.Should().BeTrue();
        alert.ReadAt.Should().Be(first);
    }
}
