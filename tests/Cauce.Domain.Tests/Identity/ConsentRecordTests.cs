using System.Security.Cryptography;
using System.Text;
using Cauce.Domain.Identity;
using FluentAssertions;

namespace Cauce.Domain.Tests.Identity;

/// <summary>
/// Pruebas de invariantes y métodos de dominio de <see cref="ConsentRecord"/>.
/// </summary>
public sealed class ConsentRecordTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Capture_ValidValues_CreatesCurrentRecord()
    {
        var hash = ComputeHash("texto");

        var record = ConsentRecord.Capture(Guid.NewGuid(), UserId, "1.0", hash, "127.0.0.1", DateTime.UtcNow);

        record.IsCurrent.Should().BeTrue();
        record.DocumentVersion.Should().Be("1.0");
    }

    [Fact]
    public void VerifyIntegrity_WithOriginalText_ReturnsTrue()
    {
        const string text = "texto de consentimiento";
        var record = ConsentRecord.Capture(Guid.NewGuid(), UserId, "1.0", ComputeHash(text), null, DateTime.UtcNow);

        record.VerifyIntegrity(text).Should().BeTrue();
    }

    [Fact]
    public void VerifyIntegrity_WithModifiedText_ReturnsFalse()
    {
        var record = ConsentRecord.Capture(Guid.NewGuid(), UserId, "1.0", ComputeHash("original"), null, DateTime.UtcNow);

        record.VerifyIntegrity("modificado").Should().BeFalse();
    }

    [Fact]
    public void Supersede_SetsIsCurrentFalse()
    {
        var record = ConsentRecord.Capture(Guid.NewGuid(), UserId, "1.0", ComputeHash("t"), null, DateTime.UtcNow);

        record.Supersede();

        record.IsCurrent.Should().BeFalse();
    }

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
