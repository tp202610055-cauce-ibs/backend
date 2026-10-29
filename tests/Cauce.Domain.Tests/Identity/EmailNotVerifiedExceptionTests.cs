using Cauce.Domain.Common.Exceptions;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;

namespace Cauce.Domain.Tests.Identity;

/// <summary>
/// Pruebas de <see cref="EmailNotVerifiedException"/>.
/// </summary>
public sealed class EmailNotVerifiedExceptionTests
{
    [Fact]
    public void Constructor_IsADomainExceptionWithAMessageForThePatient()
    {
        var exception = new EmailNotVerifiedException();

        exception.Should().BeAssignableTo<DomainException>();
        exception.Message.Should().Contain("Verifica tu correo");
    }
}
