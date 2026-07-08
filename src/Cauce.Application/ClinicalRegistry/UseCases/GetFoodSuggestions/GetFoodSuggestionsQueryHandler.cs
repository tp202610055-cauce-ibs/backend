using System.Globalization;
using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetFoodSuggestions;

/// <summary>
/// Handler de las sugerencias de alimentos del paciente (US09 CA03). Resuelve al paciente autenticado,
/// calcula una semilla determinista por paciente y semana ISO del año para la selección del catálogo, y
/// delega el armado de las tres listas al lector.
/// </summary>
public sealed class GetFoodSuggestionsQueryHandler : IRequestHandler<GetFoodSuggestionsQuery, FoodSuggestionsResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IFoodSuggestionsReader _foodSuggestionsReader;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetFoodSuggestionsQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IFoodSuggestionsReader foodSuggestionsReader)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _foodSuggestionsReader = foodSuggestionsReader;
    }

    /// <inheritdoc />
    public async Task<FoodSuggestionsResult> Handle(GetFoodSuggestionsQuery request, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var patientId = await ResolveCurrentPatientIdAsync(cancellationToken).ConfigureAwait(false);
        var catalogSeed = ComputeCatalogSeed(patientId, utcNow);

        return await _foodSuggestionsReader
            .GetAsync(patientId, utcNow, catalogSeed, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Calcula una semilla determinista y no negativa a partir del identificador del paciente y de la
    /// semana ISO del año. La misma semana produce la misma semilla; cambia cada semana.
    /// </summary>
    private static int ComputeCatalogSeed(Guid patientId, DateTime utcNow)
    {
        var weekKey = ISOWeek.GetYear(utcNow) * 100 + ISOWeek.GetWeekOfYear(utcNow);
        var accumulator = weekKey;
        foreach (var b in patientId.ToByteArray())
        {
            accumulator = unchecked(accumulator * 31 + b);
        }

        return accumulator & int.MaxValue;
    }

    private async Task<Guid> ResolveCurrentPatientIdAsync(CancellationToken ct)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), ct).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var patientRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Patient, ct).ConfigureAwait(false);
        if (user.RoleId != patientRoleId)
        {
            throw new UnauthorizedAccessException("Solo un paciente puede consultar sugerencias de alimentos.");
        }

        return user.Id;
    }
}
