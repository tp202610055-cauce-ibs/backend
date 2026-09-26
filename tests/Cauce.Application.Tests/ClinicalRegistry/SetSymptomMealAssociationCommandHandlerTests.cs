using Cauce.Application.ClinicalRegistry.UseCases.SetSymptomMealAssociation;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas del handler <see cref="SetSymptomMealAssociationCommandHandler"/>: la corrección manual fija o
/// desvincula la comida sin sujeción a la ventana de 4 horas, exige la asignación del nutricionista y que la
/// comida sea del mismo paciente, y deja su fila de auditoría antes de persistir.
/// </summary>
public sealed class SetSymptomMealAssociationCommandHandlerTests
{
    private const int NutritionistRoleId = 2;
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ISymptomRepository _symptomRepository = Substitute.For<ISymptomRepository>();
    private readonly IMealRepository _mealRepository = Substitute.For<IMealRepository>();
    private readonly INutritionistPatientRepository _assignments = Substitute.For<INutritionistPatientRepository>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<SetSymptomMealAssociationCommandHandler> _logger =
        Substitute.For<ILogger<SetSymptomMealAssociationCommandHandler>>();

    private readonly Guid _nutritionistId;
    private readonly Guid _patientId = Guid.NewGuid();

    public SetSymptomMealAssociationCommandHandlerTests()
    {
        var nutritionist = User.CreateNutritionist(Guid.NewGuid(), "kc", "n@cauce.local", "Nutri", NutritionistRoleId);
        _nutritionistId = nutritionist.Id;
        _currentUser.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(nutritionist);
        _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, Arg.Any<CancellationToken>()).Returns(NutritionistRoleId);
        _assignments.ActiveAssignmentExistsAsync(_nutritionistId, _patientId, Arg.Any<CancellationToken>()).Returns(true);
    }

    private SetSymptomMealAssociationCommandHandler CreateHandler() => new(
        _currentUser, _userRepository, _symptomRepository, _mealRepository, _assignments, _auditLogger, _unitOfWork, _logger);

    private static SetSymptomMealAssociationCommand Command(Guid symptomId, Guid? mealId) =>
        new(symptomId, mealId, Guid.NewGuid());

    private Symptom ArrangeSymptom(Guid? associatedMealId = null)
    {
        var symptom = Symptom.Report(Guid.NewGuid(), Guid.NewGuid(), _patientId, SymptomType.Bloating, 60, Now, Now, Now);
        if (associatedMealId is { } mealId)
        {
            symptom.AssociateWithMeal(mealId, Now);
        }

        _symptomRepository.FindByIdForUpdateAsync(symptom.Id, Arg.Any<CancellationToken>()).Returns(symptom);
        return symptom;
    }

    private Meal ArrangeMeal(Guid patientId, DateTime clientCreatedAt)
    {
        var meal = Meal.Register(
            Guid.NewGuid(), Guid.NewGuid(), patientId, MealTime.Lunch, clientCreatedAt, clientCreatedAt,
            new[] { new MealItemInput(Guid.NewGuid(), null, 100m, MeasurementUnit.Grams) }, Now);
        _mealRepository.FindByIdAsync(meal.Id, Arg.Any<CancellationToken>()).Returns(meal);
        return meal;
    }

    [Fact]
    public async Task Handle_MealOutsideTheFourHourWindow_AssociatesIt()
    {
        var symptom = ArrangeSymptom();
        var meal = ArrangeMeal(_patientId, Now.AddDays(-3));

        await CreateHandler().Handle(Command(symptom.Id, meal.Id), CancellationToken.None);

        symptom.AssociatedMealId.Should().Be(meal.Id);
        symptom.HasMealAssociation.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NullMeal_ClearsTheAssociation()
    {
        var symptom = ArrangeSymptom(associatedMealId: Guid.NewGuid());

        await CreateHandler().Handle(Command(symptom.Id, null), CancellationToken.None);

        symptom.AssociatedMealId.Should().BeNull();
        symptom.HasMealAssociation.Should().BeFalse();
        await _mealRepository.DidNotReceive().FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReplacingTheMeal_AuditsPreviousAndNewMealBeforeSaving()
    {
        var previousMealId = Guid.NewGuid();
        var symptom = ArrangeSymptom(associatedMealId: previousMealId);
        var meal = ArrangeMeal(_patientId, Now.AddHours(-1));

        await CreateHandler().Handle(Command(symptom.Id, meal.Id), CancellationToken.None);

        Received.InOrder(() =>
        {
            _auditLogger.LogAsync(
                AuditActionType.MealAssociationCorrection,
                Arg.Is(nameof(Symptom)),
                symptom.Id,
                Arg.Is<string?>(hash => hash == null),
                Arg.Is<string?>(hash => hash == null),
                Arg.Is<string?>(context => context != null
                    && context.Contains($"\"previous_meal_id\":\"{previousMealId}\"")
                    && context.Contains($"\"new_meal_id\":\"{meal.Id}\"")),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_ClearingTheAssociation_AuditsNullAsTheNewMeal()
    {
        var previousMealId = Guid.NewGuid();
        var symptom = ArrangeSymptom(associatedMealId: previousMealId);

        await CreateHandler().Handle(Command(symptom.Id, null), CancellationToken.None);

        await _auditLogger.Received(1).LogAsync(
            AuditActionType.MealAssociationCorrection,
            nameof(Symptom),
            symptom.Id,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Is<string?>(context => context != null
                && context.Contains($"\"previous_meal_id\":\"{previousMealId}\"")
                && context.Contains("\"new_meal_id\":null")),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SameMealAsBefore_StillAuditsAndSaves()
    {
        var meal = ArrangeMeal(_patientId, Now.AddHours(-1));
        var symptom = ArrangeSymptom(associatedMealId: meal.Id);

        await CreateHandler().Handle(Command(symptom.Id, meal.Id), CancellationToken.None);

        // Confirmar la misma comida es una decisión clínica explícita: queda en la bitácora aunque el
        // síntoma no cambie.
        await _auditLogger.Received(1).LogAsync(
            AuditActionType.MealAssociationCorrection,
            nameof(Symptom),
            symptom.Id,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Is<string?>(context => context != null
                && context.Contains($"\"previous_meal_id\":\"{meal.Id}\"")
                && context.Contains($"\"new_meal_id\":\"{meal.Id}\"")),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SymptomNotFound_Throws()
    {
        _symptomRepository.FindByIdForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Symptom?)null);

        var act = () => CreateHandler().Handle(Command(Guid.NewGuid(), null), CancellationToken.None);

        await act.Should().ThrowAsync<SymptomNotFoundException>();
    }

    [Fact]
    public async Task Handle_NutritionistNotAssigned_ThrowsWithoutTouchingTheSymptom()
    {
        var originalMealId = Guid.NewGuid();
        var symptom = ArrangeSymptom(associatedMealId: originalMealId);
        _assignments.ActiveAssignmentExistsAsync(_nutritionistId, _patientId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => CreateHandler().Handle(Command(symptom.Id, null), CancellationToken.None);

        await act.Should().ThrowAsync<PatientAccessNotAuthorizedException>();
        symptom.AssociatedMealId.Should().Be(originalMealId);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MealOfAnotherPatient_ThrowsWithoutTouchingTheSymptom()
    {
        var originalMealId = Guid.NewGuid();
        var symptom = ArrangeSymptom(associatedMealId: originalMealId);
        var foreignMeal = ArrangeMeal(Guid.NewGuid(), Now.AddHours(-1));

        var act = () => CreateHandler().Handle(Command(symptom.Id, foreignMeal.Id), CancellationToken.None);

        await act.Should().ThrowAsync<PatientResourceAccessException>();
        symptom.AssociatedMealId.Should().Be(originalMealId);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownMeal_Throws()
    {
        var symptom = ArrangeSymptom();
        _mealRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Meal?)null);

        var act = () => CreateHandler().Handle(Command(symptom.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<MealNotFoundException>();
    }
}
