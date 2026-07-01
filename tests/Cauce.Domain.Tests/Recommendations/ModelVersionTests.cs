using Cauce.Domain.Recommendations;
using FluentAssertions;

namespace Cauce.Domain.Tests.Recommendations;

/// <summary>
/// Pruebas de la entidad <see cref="ModelVersion"/>.
/// </summary>
public sealed class ModelVersionTests
{
    private static ModelVersion Register() =>
        ModelVersion.Register("rule-v1.0.0", "ABC123", 250_000, "{}", "system-seeder", DateTime.UtcNow);

    [Fact]
    public void Register_SetsAllFields()
    {
        var version = Register();

        version.VersionName.Should().Be("rule-v1.0.0");
        version.ModelHash.Should().Be("ABC123");
        version.TrainingDatasetSize.Should().Be(250_000);
        version.DeployedBy.Should().Be("system-seeder");
        version.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Register_WithEmptyName_Throws()
    {
        var act = () => ModelVersion.Register(" ", "hash", null, "{}", "seeder", DateTime.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var version = Register();

        version.Activate();

        version.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var version = Register();
        version.Activate();

        version.Deactivate();

        version.IsActive.Should().BeFalse();
    }

    [Fact]
    public void VerifyIntegrity_WithMatchingHash_ReturnsTrue()
    {
        Register().VerifyIntegrity("abc123").Should().BeTrue();
    }

    [Fact]
    public void VerifyIntegrity_WithDifferentHash_ReturnsFalse()
    {
        Register().VerifyIntegrity("different").Should().BeFalse();
    }
}
