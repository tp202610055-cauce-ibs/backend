using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using FluentAssertions;

namespace Cauce.Domain.Tests.Patients;

/// <summary>
/// Pruebas de invariantes y métodos de dominio de <see cref="NutritionistPatient"/>.
/// </summary>
public sealed class NutritionistPatientTests
{
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly Guid NutritionistId = Guid.NewGuid();
    private static readonly Guid PatientId = Guid.NewGuid();

    [Fact]
    public void Establish_CreatesActiveAssignment()
    {
        var assignment = NutritionistPatient.Establish(Guid.NewGuid(), NutritionistId, PatientId, Guid.NewGuid(), Now);

        assignment.Status.Should().Be(AssignmentStatus.Active);
        assignment.IsActive().Should().BeTrue();
        assignment.UnassignedAt.Should().BeNull();
    }

    [Fact]
    public void Deactivate_ChangesToInactiveAndSetsUnassignedAt()
    {
        var assignment = NutritionistPatient.Establish(Guid.NewGuid(), NutritionistId, PatientId, null, Now);

        assignment.Deactivate(Now.AddDays(1));

        assignment.Status.Should().Be(AssignmentStatus.Inactive);
        assignment.UnassignedAt.Should().Be(Now.AddDays(1));
    }

    [Fact]
    public void Deactivate_AlreadyInactive_Throws()
    {
        var assignment = NutritionistPatient.Establish(Guid.NewGuid(), NutritionistId, PatientId, null, Now);
        assignment.Deactivate(Now.AddDays(1));

        var act = () => assignment.Deactivate(Now.AddDays(2));

        act.Should().Throw<InvalidOperationException>();
    }
}
