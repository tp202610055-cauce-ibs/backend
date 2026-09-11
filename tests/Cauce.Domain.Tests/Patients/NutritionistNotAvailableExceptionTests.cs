using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Patients.Exceptions;
using FluentAssertions;

namespace Cauce.Domain.Tests.Patients;

/// <summary>
/// Pruebas de <see cref="NutritionistNotAvailableException"/>. El valor de <c>Reason</c> es parte del
/// contrato: viaja en la extensión <c>reason</c> del envelope y el cliente lo lee para afinar el mensaje
/// (actas A41 y A53).
/// </summary>
public sealed class NutritionistNotAvailableExceptionTests
{
    [Theory]
    [InlineData(UserStatus.PendingActivation, "pending_activation")]
    [InlineData(UserStatus.Inactive, "inactive")]
    [InlineData(UserStatus.Suspended, "suspended")]
    [InlineData(UserStatus.Active, "unavailable")]
    public void Reason_EachStatus_MapsToItsStableValue(UserStatus status, string expectedReason)
    {
        var exception = new NutritionistNotAvailableException(status);

        exception.Status.Should().Be(status);
        exception.Reason.Should().Be(expectedReason);
    }
}
