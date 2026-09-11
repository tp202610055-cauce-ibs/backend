using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;

namespace Cauce.Domain.Tests.Identity;

/// <summary>
/// Pruebas de <see cref="NutritionistNotPendingActivationException"/> (acta A52).
/// </summary>
public sealed class NutritionistNotPendingActivationExceptionTests
{
    [Theory]
    [InlineData(UserStatus.Active)]
    [InlineData(UserStatus.Suspended)]
    [InlineData(UserStatus.Inactive)]
    public void Constructor_KeepsTheStatusThatBlockedTheResend(UserStatus status)
    {
        var exception = new NutritionistNotPendingActivationException(status);

        exception.Status.Should().Be(status);
        exception.Message.Should().NotBeNullOrWhiteSpace();
    }
}
