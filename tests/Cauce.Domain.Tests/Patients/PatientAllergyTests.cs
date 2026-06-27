using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Domain.Patients.Exceptions;
using FluentAssertions;

namespace Cauce.Domain.Tests.Patients;

/// <summary>
/// Pruebas de invariantes y métodos de dominio de <see cref="PatientAllergy"/>.
/// </summary>
public sealed class PatientAllergyTests
{
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly Guid PatientId = Guid.NewGuid();
    private static readonly Guid AllergyId = Guid.NewGuid();

    [Fact]
    public void Declare_ValidValues_CreatesDeclaration()
    {
        var declaration = PatientAllergy.Declare(Guid.NewGuid(), PatientId, AllergyId, AllergySeverity.Moderate, "nota", Now);

        declaration.PatientId.Should().Be(PatientId);
        declaration.AllergyId.Should().Be(AllergyId);
        declaration.Severity.Should().Be(AllergySeverity.Moderate);
    }

    [Fact]
    public void Declare_NotesTooLong_Throws()
    {
        var longNotes = new string('x', 501);
        var act = () => PatientAllergy.Declare(Guid.NewGuid(), PatientId, AllergyId, AllergySeverity.Mild, longNotes, Now);

        act.Should().Throw<InvalidBiometricValueException>();
    }

    [Fact]
    public void UpdateSeverity_ChangesSeverity()
    {
        var declaration = PatientAllergy.Declare(Guid.NewGuid(), PatientId, AllergyId, AllergySeverity.Mild, null, Now);

        declaration.UpdateSeverity(AllergySeverity.Severe, Now.AddHours(1));

        declaration.Severity.Should().Be(AllergySeverity.Severe);
    }
}
