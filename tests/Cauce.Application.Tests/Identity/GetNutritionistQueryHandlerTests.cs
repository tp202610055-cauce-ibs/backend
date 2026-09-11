using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.GetNutritionist;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;
using NSubstitute;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del handler <see cref="GetNutritionistQueryHandler"/> (acta A49).
/// </summary>
public sealed class GetNutritionistQueryHandlerTests
{
    private const int PatientRoleId = 1;
    private const int NutritionistRoleId = 2;

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();

    private GetNutritionistQueryHandler CreateHandler()
    {
        _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, Arg.Any<CancellationToken>()).Returns(NutritionistRoleId);
        return new GetNutritionistQueryHandler(_userRepository);
    }

    [Fact]
    public async Task Handle_ExistingNutritionist_ReturnsTheSummaryWithItsStatus()
    {
        var nutritionist = User.CreateNutritionist(Guid.NewGuid(), "kc-nutri", "n@cauce.local", "Nutri Demo", NutritionistRoleId);
        _userRepository.FindByIdAsync(nutritionist.Id, Arg.Any<CancellationToken>()).Returns(nutritionist);

        var result = await CreateHandler().Handle(new GetNutritionistQuery(nutritionist.Id), CancellationToken.None);

        result.Should().Be(new GetNutritionistResult(
            nutritionist.Id, "n@cauce.local", "Nutri Demo", UserStatus.PendingActivation));
    }

    [Fact]
    public async Task Handle_UnknownIdentifier_ThrowsNutritionistNotFound()
    {
        _userRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var act = () => CreateHandler().Handle(new GetNutritionistQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NutritionistNotFoundException>();
    }

    [Fact]
    public async Task Handle_PatientAccount_ThrowsNutritionistNotFound()
    {
        var patient = User.CreatePatient(Guid.NewGuid(), "kc-patient", "p@cauce.local", "Paciente", PatientRoleId);
        _userRepository.FindByIdAsync(patient.Id, Arg.Any<CancellationToken>()).Returns(patient);

        var act = () => CreateHandler().Handle(new GetNutritionistQuery(patient.Id), CancellationToken.None);

        // Una cuenta de otro rol responde igual que una inexistente.
        await act.Should().ThrowAsync<NutritionistNotFoundException>();
    }
}
