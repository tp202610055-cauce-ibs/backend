using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cauce.Api.Application.DTOs.Requests;
using Cauce.Api.Application.DTOs.Responses;
using Cauce.Api.Application.Exceptions;
using Cauce.Api.Application.Interfaces;
using Cauce.Api.Configuration;
using Cauce.Api.Domain.Entities;
using Cauce.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Cauce.Api.Application.Services;

/// <summary>
/// Implementación del servicio de autenticación. Materializa US01 y US05.
///
/// Concesiones DEC-011 aplicadas:
/// - Email verification automático (EmailConfirmed = true al registrar).
/// - Sin persistencia de consent_records (tabla diferida a v0.2.0).
/// - Sin asignación a nutricionista en registro (US20 CA02 lo permite).
/// - Audit log mediante ILogger en lugar de tabla audit_logs.
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ApplicationDbContext _db;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ApplicationDbContext db,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterPatientAsync(RegisterRequest request, CancellationToken ct = default)
    {
        // Validación 1: unicidad de email (US01 CA02).
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            _logger.LogWarning("Intento de registro con correo duplicado: {Email}", request.Email);
            throw new DuplicateEmailException(request.Email);
        }

        // Carga del rol "patient" desde el catálogo (diseño OE2: cada usuario tiene exactamente un rol).
        var patientRole = await _db.AppUserRoles
            .FirstAsync(r => r.RoleName == "patient", ct);

        // Construcción de la entidad User.
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            RoleId = patientRole.RoleId,
            Status = "active", // En MVP la cuenta queda activa directamente (sin verificación SMTP).
            EmailConfirmed = true, // DEC-011: email verification automático.
            CreatedAt = now,
            UpdatedAt = now
        };

        // Creación con UserManager (hashea la contraseña y valida la política).
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            _logger.LogWarning("Registro rechazado por política de Identity: {Errors}", string.Join("; ", errors));
            throw new WeakPasswordException(errors);
        }

        _logger.LogInformation("Paciente registrado correctamente: {Email}, UserId={UserId}", user.Email, user.Id);

        // Generación del token JWT.
        var (token, expiresAt) = GenerateJwtToken(user, patientRole.RoleName);

        return new AuthResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            Role = patientRole.RoleName,
            Token = token,
            ExpiresAt = expiresAt
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        // Búsqueda del usuario.
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Por seguridad, no revelar si el email existe en el sistema (US05 CA02 y US06 CA02).
            _logger.LogWarning("Intento de login con correo inexistente: {Email}", request.Email);
            throw new InvalidCredentialsException();
        }

        // SignInManager.CheckPasswordSignInAsync con lockoutOnFailure: true
        // gestiona automáticamente el contador de intentos fallidos (failed_login_attempts)
        // y el bloqueo al alcanzar el umbral configurado en Program.cs (5 intentos).
        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Login rechazado por cuenta bloqueada: {Email}, UserId={UserId}", user.Email, user.Id);
            throw new AccountLockedException(user.LockoutEnd ?? DateTimeOffset.UtcNow.AddMinutes(15));
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("Login fallido por credenciales inválidas: {Email}, UserId={UserId}", user.Email, user.Id);
            throw new InvalidCredentialsException();
        }

        // Login exitoso: actualizar last_login_at.
        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // Cargar el rol para incluirlo en la respuesta y los claims.
        var role = await _db.AppUserRoles.FirstAsync(r => r.RoleId == user.RoleId, ct);

        _logger.LogInformation("Login exitoso: {Email}, UserId={UserId}, Role={Role}", user.Email, user.Id, role.RoleName);

        var (token, expiresAt) = GenerateJwtToken(user, role.RoleName);

        return new AuthResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            Role = role.RoleName,
            Token = token,
            ExpiresAt = expiresAt
        };
    }

    /// <summary>
    /// Genera un token JWT firmado con HMAC-SHA256, incluyendo los claims
    /// estándar y los específicos del dominio (userId, email, role).
    /// </summary>
    private (string Token, DateTime ExpiresAt) GenerateJwtToken(User user, string roleName)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, roleName)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return (tokenString, expiresAt);
    }
}