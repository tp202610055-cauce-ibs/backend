using Cauce.Application.Common.Interfaces.Identity;

namespace Cauce.Application.Common.Identity;

/// <summary>
/// Implementación con alcance de petición (scoped) de <see cref="IAuthenticationAttemptContext"/>.
/// </summary>
public sealed class AuthenticationAttemptContext : IAuthenticationAttemptContext
{
    /// <inheritdoc />
    public string? FailureCause { get; private set; }

    /// <inheritdoc />
    public void RecordFailure(string cause)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cause);
        FailureCause = cause;
    }
}
