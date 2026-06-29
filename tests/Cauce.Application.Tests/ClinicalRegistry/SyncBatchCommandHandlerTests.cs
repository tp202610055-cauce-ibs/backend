using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.UseCases.CreateMeal;
using Cauce.Application.ClinicalRegistry.UseCases.CreateSymptom;
using Cauce.Application.ClinicalRegistry.UseCases.SyncBatch;
using Cauce.Application.Common.Idempotency;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.ClinicalRegistry.Enums;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Cauce.Application.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas del handler <see cref="SyncBatchCommandHandler"/> y su clasificación de
/// elementos en aceptados, duplicados y errores.
/// </summary>
public sealed class SyncBatchCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly ISender _mediator = Substitute.For<ISender>();
    private readonly IIdempotencyContext _idempotencyContext = Substitute.For<IIdempotencyContext>();
    private readonly IMealRepository _mealRepository = Substitute.For<IMealRepository>();
    private readonly ISymptomRepository _symptomRepository = Substitute.For<ISymptomRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly ILogger<SyncBatchCommandHandler> _logger = Substitute.For<ILogger<SyncBatchCommandHandler>>();

    private SyncBatchCommandHandler CreateHandler() => new(
        _mediator, _idempotencyContext, _mealRepository, _symptomRepository, _unitOfWork, _auditLogger, _logger);

    private static MealBatchItem Meal() => new(
        Guid.NewGuid(), MealTime.Lunch, Now, Now,
        new[] { new MealItemRequest(Guid.NewGuid(), null, 100m, MeasurementUnit.Grams) });

    private static SymptomBatchItem Symptom() => new(Guid.NewGuid(), SymptomType.Bloating, 50, Now, Now);

    [Fact]
    public async Task Handle_AcceptedMealAndErrorSymptom_ClassifiesAndAudits()
    {
        _mediator.Send(Arg.Any<CreateMealCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _idempotencyContext.WasReplay = false;
                return new CreateMealResult(Guid.NewGuid(), FodmapLevel.Low);
            });
        _mediator.Send(Arg.Any<CreateSymptomCommand>(), Arg.Any<CancellationToken>())
            .Throws(new ValidationException("intensidad inválida"));

        var command = new SyncBatchCommand(new[] { Meal() }, new[] { Symptom() });
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.Accepted.Should().HaveCount(1);
        result.Errors.Should().HaveCount(1);
        result.Errors[0].ErrorCode.Should().Be("validation_error");
        await _auditLogger.Received(1).LogAsync(
            AuditActionType.Create, "SyncBatch", Arg.Any<Guid?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReplayedMeal_ClassifiedAsDuplicate()
    {
        _mediator.Send(Arg.Any<CreateMealCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _idempotencyContext.WasReplay = true;
                return new CreateMealResult(Guid.NewGuid(), FodmapLevel.Low);
            });

        var command = new SyncBatchCommand(new[] { Meal() }, Array.Empty<SymptomBatchItem>());
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.Duplicates.Should().HaveCount(1);
        result.Accepted.Should().BeEmpty();
    }
}
