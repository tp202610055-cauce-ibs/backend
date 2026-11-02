using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.Services;
using Cauce.Domain.Identity.Exceptions;
using MediatR;

namespace Cauce.Application.Identity.UseCases.ValidatePasswordResetToken;

/// <summary>
/// Handler de <see cref="ValidatePasswordResetTokenQuery"/>. Aplica las mismas reglas que la
/// confirmación, pero responde igual para token inexistente, vencido o usado y nunca lo consume.
/// </summary>
public sealed class ValidatePasswordResetTokenQueryHandler : IRequestHandler<ValidatePasswordResetTokenQuery>
{
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public ValidatePasswordResetTokenQueryHandler(
        IPasswordResetTokenRepository tokenRepository,
        IUserRepository userRepository)
    {
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
    }

    /// <inheritdoc />
    public async Task Handle(ValidatePasswordResetTokenQuery request, CancellationToken cancellationToken)
    {
        var verification = await PasswordResetTokenVerifier
            .VerifyAsync(request.Token, _tokenRepository, _userRepository, DateTime.UtcNow, cancellationToken)
            .ConfigureAwait(false);

        if (verification.State != PasswordResetTokenState.Valid)
        {
            throw new InvalidPasswordResetTokenException();
        }
    }
}
