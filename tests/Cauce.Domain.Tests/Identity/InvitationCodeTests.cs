using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;

namespace Cauce.Domain.Tests.Identity;

/// <summary>
/// Pruebas de invariantes y métodos de dominio de <see cref="InvitationCode"/>.
/// </summary>
public sealed class InvitationCodeTests
{
    private static readonly Guid Nutritionist = Guid.NewGuid();

    [Fact]
    public void Generate_ValidValues_CreatesActiveCodeWith72hValidity()
    {
        var now = DateTime.UtcNow;

        var code = InvitationCode.Generate(Guid.NewGuid(), "ABCDEFGH", Nutritionist, now, InvitationCode.Validity);

        code.Status.Should().Be(InvitationStatus.Active);
        code.ExpiresAt.Should().Be(now + TimeSpan.FromHours(72));
    }

    [Fact]
    public void Generate_LowercaseCode_ThrowsArgumentException()
    {
        var act = () => InvitationCode.Generate(Guid.NewGuid(), "abcdefgh", Nutritionist, DateTime.UtcNow, InvitationCode.Validity);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Generate_WrongValidity_ThrowsArgumentException()
    {
        var act = () => InvitationCode.Generate(Guid.NewGuid(), "ABCDEFGH", Nutritionist, DateTime.UtcNow, TimeSpan.FromHours(24));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsValid_ActiveAndNotExpired_ReturnsTrue()
    {
        var now = DateTime.UtcNow;
        var code = InvitationCode.Generate(Guid.NewGuid(), "ABCDEFGH", Nutritionist, now, InvitationCode.Validity);

        code.IsValid(now.AddHours(1)).Should().BeTrue();
        code.IsValid(now.AddHours(73)).Should().BeFalse();
    }

    [Fact]
    public void MarkAsUsed_ActiveCode_TransitionsToUsed()
    {
        var now = DateTime.UtcNow;
        var patient = Guid.NewGuid();
        var code = InvitationCode.Generate(Guid.NewGuid(), "ABCDEFGH", Nutritionist, now, InvitationCode.Validity);

        code.MarkAsUsed(patient, now.AddHours(1));

        code.Status.Should().Be(InvitationStatus.Used);
        code.UsedByPatientId.Should().Be(patient);
    }

    [Fact]
    public void MarkAsUsed_AlreadyUsedCode_ThrowsAlreadyUsed()
    {
        var now = DateTime.UtcNow;
        var code = InvitationCode.Generate(Guid.NewGuid(), "ABCDEFGH", Nutritionist, now, InvitationCode.Validity);
        code.MarkAsUsed(Guid.NewGuid(), now.AddHours(1));

        var act = () => code.MarkAsUsed(Guid.NewGuid(), now.AddHours(2));

        act.Should().Throw<InvitationCodeAlreadyUsedException>();
    }

    [Fact]
    public void MarkAsUsed_ExpiredCode_ThrowsExpired()
    {
        var now = DateTime.UtcNow;
        var code = InvitationCode.Generate(Guid.NewGuid(), "ABCDEFGH", Nutritionist, now, InvitationCode.Validity);

        var act = () => code.MarkAsUsed(Guid.NewGuid(), now.AddHours(73));

        act.Should().Throw<ExpiredInvitationCodeException>();
    }
}
