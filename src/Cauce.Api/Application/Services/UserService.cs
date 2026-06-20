using Cauce.Api.Application.DTOs.Users;
using Cauce.Api.Application.Exceptions;
using Cauce.Api.Application.Interfaces;
using Cauce.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.Application.Services;

/// <summary>
/// Implementación del servicio de aplicación para consultas de perfil de usuario.
/// </summary>
public class UserService : IUserService
{
    private readonly UserManager<User> _userManager;

    /// <summary>
    /// Inicializa una nueva instancia de UserService.
    /// </summary>
    /// <param name="userManager">Gestor de usuarios de ASP.NET Core Identity.</param>
    public UserService(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    /// <inheritdoc />
    public async Task<UserProfileResponse> GetProfileAsync(Guid userId)
    {
        var user = await _userManager.Users
            .Include(u => u.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            throw new UserNotFoundException(userId);
        }

        return new UserProfileResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            EmailVerified = user.EmailConfirmed,
            Role = user.Role.RoleName,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }
}