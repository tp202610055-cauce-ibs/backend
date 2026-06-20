using Cauce.Api.Application.DTOs.Requests;
using Cauce.Api.Application.DTOs.Responses;

namespace Cauce.Api.Application.Interfaces;

/// <summary>
/// Contrato del servicio de autenticación. Encapsula los flujos de registro
/// e inicio de sesión definidos en US01 y US05 del Product Backlog v5.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registra un nuevo paciente en el sistema (US01).
    /// </summary>
    /// <exception cref="Exceptions.DuplicateEmailException">Cuando el correo ya existe.</exception>
    /// <exception cref="Exceptions.WeakPasswordException">Cuando la contraseña no cumple la política.</exception>
    Task<AuthResponse> RegisterPatientAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>
    /// Autentica a un usuario existente y emite un token JWT (US05).
    /// </summary>
    /// <exception cref="Exceptions.InvalidCredentialsException">Cuando las credenciales son inválidas.</exception>
    /// <exception cref="Exceptions.AccountLockedException">Cuando la cuenta está bloqueada por intentos fallidos.</exception>
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}