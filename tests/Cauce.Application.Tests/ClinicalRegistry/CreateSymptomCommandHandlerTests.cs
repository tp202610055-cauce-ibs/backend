using Cauce.Application.ClinicalRegistry.UseCases.CreateSymptom;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas del handler <see cref="CreateSymptomCommandHandler"/>, en particular la
/// correlación temporal con la comida más reciente dentro de la ventana de 4 horas.
/// </summary>
public sealed class CreateSymptomCommandHandlerTests
{
    private const int PatientRoleId = 1;
    private static readonly DateTime Now = new(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ISymptomRepository _symptomRepository = Substitute.For<ISymptomRepository>();
    private readonly IMealRepository _mealRepository = Substitute.For<IMealRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<CreateSymptomCommandHandler> _logger = Substitute.For<ILogger<CreateSymptomCommandHandler>>();

    private CreateSymptomCommandHandler CreateHandler() => new(
        _currentUserService, _userRepository, _symptomRepository, _mealRepository, _unitOfWork, _logger);

    private void ArrangePatient()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc", "p@cauce.local", "Paciente", PatientRoleId);
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.GetRoleIdAsync(UserRoles.Patient, Arg.Any<CancellationToken>()).Returns(PatientRoleId);
    }

    private static CreateSymptomCommand Command() => new(Guid.NewGuid(), SymptomType.Bloating, 60, Now, Now);

    [Fact]
    public async Task Handle_MealWithinWindow_AssociatesMeal()
    {
        ArrangePatient();
        var meal = Meal.Register(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), MealTime.Lunch, Now.AddHours(-2), Now.AddHours(-2),
            new[] { new MealItemInput(Guid.NewGuid(), null, 100m, MeasurementUnit.Grams) }, Now);
        _mealRepository.FindLatestInWindowAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(meal);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        result.HasMealAssociation.Should().BeTrue();
        result.AssociatedMealId.Should().Be(meal.Id);
    }

    [Fact]
    public async Task Handle_NoMealWithinWindow_DoesNotAssociate()
    {
        ArrangePatient();
        _mealRepository.FindLatestInWindowAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((Meal?)null);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        result.HasMealAssociation.Should().BeFalse();
        result.AssociatedMealId.Should().BeNull();
        await _symptomRepository.Received(1).AddAsync(Arg.Any<Symptom>(), Arg.Any<CancellationToken>());
    }
}
