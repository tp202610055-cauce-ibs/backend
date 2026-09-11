using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using MediatR;

namespace Cauce.Application.Identity.UseCases.GetNutritionist;

/// <summary>
/// Handler de la consulta administrativa de un nutricionista (acta A49). Permite verificar si la cuenta ya
/// se activó sin consultar la base de datos, y es el recurso al que apunta el <c>Location</c> de la
/// provisión.
/// </summary>
public sealed class GetNutritionistQueryHandler : IRequestHandler<GetNutritionistQuery, GetNutritionistResult>
{
    private readonly IUserRepository _userRepository;

    /// <summary>
    /// Inicializa el handler con el repositorio de cuentas.
    /// </summary>
    /// <param name="userRepository">Repositorio de cuentas.</param>
    public GetNutritionistQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <inheritdoc />
    public async Task<GetNutritionistResult> Handle(GetNutritionistQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository
            .FindByIdAsync(request.NutritionistId, cancellationToken)
            .ConfigureAwait(false);
        var nutritionistRoleId = await _userRepository
            .GetRoleIdAsync(UserRoles.Nutritionist, cancellationToken)
            .ConfigureAwait(false);

        // Una cuenta de otro rol responde igual que una inexistente, para no revelar qué identificadores
        // existen.
        if (user is null || user.RoleId != nutritionistRoleId)
        {
            throw new NutritionistNotFoundException();
        }

        return new GetNutritionistResult(user.Id, user.Email, user.FullName, user.Status);
    }
}
