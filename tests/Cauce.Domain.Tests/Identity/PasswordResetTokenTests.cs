using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;

namespace Cauce.Domain.Tests.Identity;

/// <summary>
/// Pruebas de invariantes y métodos de dominio de <see cref="PasswordResetToken"/>.
/// </summary>
public sealed class PasswordResetTokenTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Issue_ValidValues_CreatesUsableTokenWith30minValidity()
    {
        var now = DateTime.UtcNow;

        var token = PasswordResetToken.Issue(Guid.NewGuid(), UserId, "hash", now, PasswordResetToken.Validity);

        token.ExpiresAt.Should().Be(now + TimeSpan.FromMinutes(30));
        token.IsUsable(now).Should().BeTrue();
    }

    [Fact]
    public void Issue_WrongValidity_ThrowsArgumentException()
    {
        var act = () => PasswordResetToken.Issue(Guid.NewGuid(), UserId, "hash", DateTime.UtcNow, TimeSpan.FromHours(1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Consume_UsableToken_MarksAsUsed()
    {
        var now = DateTime.UtcNow;
        var token = PasswordResetToken.Issue(Guid.NewGuid(), UserId, "hash", now, PasswordResetToken.Validity);

        token.Consume(now.AddMinutes(5));

        token.IsUsed.Should().BeTrue();
        token.UsedAt.Should().Be(now.AddMinutes(5));
    }

    [Fact]
    public void Consume_AlreadyUsedToken_ThrowsInvalid()
    {
        var now = DateTime.UtcNow;
        var token = PasswordResetToken.Issue(Guid.NewGuid(), UserId, "hash", now, PasswordResetToken.Validity);
        token.Consume(now.AddMinutes(1));

        var act = () => token.Consume(now.AddMinutes(2));

        act.Should().Throw<InvalidPasswordResetTokenException>();
    }

    [Fact]
    public void Consume_ExpiredToken_ThrowsExpired()
    {
        var now = DateTime.UtcNow;
        var token = PasswordResetToken.Issue(Guid.NewGuid(), UserId, "hash", now, PasswordResetToken.Validity);

        var act = () => token.Consume(now.AddMinutes(31));

        act.Should().Throw<ExpiredPasswordResetTokenException>();
    }
}
