using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.UseCases.CreateMeal;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using Cauce.Domain.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas del handler <see cref="CreateMealCommandHandler"/>.
/// </summary>
public sealed class CreateMealCommandHandlerTests
{
    private const int PatientRoleId = 1;
    private static readonly DateTime Now = new(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IMealRepository _mealRepository = Substitute.For<IMealRepository>();
    private readonly IFoodItemRepository _foodItemRepository = Substitute.For<IFoodItemRepository>();
    private readonly ICustomFoodRepository _customFoodRepository = Substitute.For<ICustomFoodRepository>();
    private readonly IFodmapAggregator _fodmapAggregator = Substitute.For<IFodmapAggregator>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<CreateMealCommandHandler> _logger = Substitute.For<ILogger<CreateMealCommandHandler>>();

    private CreateMealCommandHandler CreateHandler() => new(
        _currentUserService, _userRepository, _mealRepository, _foodItemRepository,
        _customFoodRepository, _fodmapAggregator, _unitOfWork, _logger);

    private User ArrangePatient()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc", "p@cauce.local", "Paciente", PatientRoleId);
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.GetRoleIdAsync(UserRoles.Patient, Arg.Any<CancellationToken>()).Returns(PatientRoleId);
        _fodmapAggregator.AggregateForMeal(Arg.Any<IEnumerable<(FodmapLevel, decimal)>>()).Returns(FodmapLevel.Low);
        return user;
    }

    private static CreateMealCommand Command(Guid? foodId = null, Guid? customFoodId = null)
    {
        var item = customFoodId.HasValue
            ? new MealItemRequest(null, customFoodId, 100m, MeasurementUnit.Grams)
            : new MealItemRequest(foodId ?? Guid.NewGuid(), null, 100m, MeasurementUnit.Grams);
        return new CreateMealCommand(Guid.NewGuid(), MealTime.Lunch, Now, Now, new[] { item });
    }

    [Fact]
    public async Task Handle_WithActiveFoodItem_PersistsMealAndReturnsFodmap()
    {
        ArrangePatient();
        var foodId = Guid.NewGuid();
        var food = FoodItem.SeedEntry(foodId, "Arroz", "cereales", 130m, 2.7m, 28m, 0.3m, 0.4m, FodmapLevel.Low, null, false, Now);
        _foodItemRepository.FindByIdAsync(foodId, Arg.Any<CancellationToken>()).Returns(food);

        var result = await CreateHandler().Handle(Command(foodId: foodId), CancellationToken.None);

        result.MealId.Should().NotBeEmpty();
        result.AggregatedFodmap.Should().Be(FodmapLevel.Low);
        await _mealRepository.Received(1).AddAsync(Arg.Any<Meal>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithOwnCustomFood_PersistsMeal()
    {
        var user = ArrangePatient();
        var customFoodId = Guid.NewGuid();
        var customFood = CustomFood.Create(customFoodId, user.Id, "Mi plato", 200m, Now);
        _customFoodRepository.FindByIdAsync(customFoodId, Arg.Any<CancellationToken>()).Returns(customFood);

        var result = await CreateHandler().Handle(Command(customFoodId: customFoodId), CancellationToken.None);

        result.MealId.Should().NotBeEmpty();
        await _mealRepository.Received(1).AddAsync(Arg.Any<Meal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInactiveFoodItem_ThrowsFoodItemNotFound()
    {
        ArrangePatient();
        var foodId = Guid.NewGuid();
        var food = FoodItem.SeedEntry(foodId, "Arroz", "cereales", 130m, 2.7m, 28m, 0.3m, 0.4m, FodmapLevel.Low, null, false, Now);
        food.Deactivate(Now);
        _foodItemRepository.FindByIdAsync(foodId, Arg.Any<CancellationToken>()).Returns(food);

        var act = () => CreateHandler().Handle(Command(foodId: foodId), CancellationToken.None);

        await act.Should().ThrowAsync<FoodItemNotFoundException>();
    }

    [Fact]
    public async Task Handle_WithOtherPatientsCustomFood_ThrowsResourceAccess()
    {
        ArrangePatient();
        var customFoodId = Guid.NewGuid();
        var customFood = CustomFood.Create(customFoodId, Guid.NewGuid(), "Ajeno", 200m, Now);
        _customFoodRepository.FindByIdAsync(customFoodId, Arg.Any<CancellationToken>()).Returns(customFood);

        var act = () => CreateHandler().Handle(Command(customFoodId: customFoodId), CancellationToken.None);

        await act.Should().ThrowAsync<PatientResourceAccessException>();
    }
}
