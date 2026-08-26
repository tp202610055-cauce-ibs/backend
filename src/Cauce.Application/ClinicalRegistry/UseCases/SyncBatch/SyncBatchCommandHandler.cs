using System.Text.Json;
using Cauce.Application.ClinicalRegistry.UseCases.CreateMeal;
using Cauce.Application.ClinicalRegistry.UseCases.CreateSymptom;
using Cauce.Application.Common.Exceptions;
using Cauce.Application.Common.Idempotency;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using Cauce.Domain.Common.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.ClinicalRegistry.UseCases.SyncBatch;

/// <summary>
/// Handler de la sincronización por lotes. Procesa cada comida y síntoma como un
/// subcomando idempotente individual y agrupa los resultados en aceptados, duplicados y
/// errores. Registra un único evento de auditoría con los conteos del lote. Si KeyDB no
/// está disponible, la restricción única de <c>client_guid</c> actúa como red de
/// seguridad: el duplicado se resuelve consultando el registro existente.
/// </summary>
public sealed class SyncBatchCommandHandler : IRequestHandler<SyncBatchCommand, SyncBatchResult>
{
    private readonly ISender _mediator;
    private readonly IIdempotencyContext _idempotencyContext;
    private readonly IMealRepository _mealRepository;
    private readonly ISymptomRepository _symptomRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<SyncBatchCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public SyncBatchCommandHandler(
        ISender mediator,
        IIdempotencyContext idempotencyContext,
        IMealRepository mealRepository,
        ISymptomRepository symptomRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        ILogger<SyncBatchCommandHandler> logger)
    {
        _mediator = mediator;
        _idempotencyContext = idempotencyContext;
        _mealRepository = mealRepository;
        _symptomRepository = symptomRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SyncBatchResult> Handle(SyncBatchCommand request, CancellationToken cancellationToken)
    {
        var accepted = new List<SyncAcceptedEntry>();
        var duplicates = new List<SyncDuplicateEntry>();
        var errors = new List<SyncErrorEntry>();
        var mealsAccepted = 0;
        var symptomsAccepted = 0;

        foreach (var meal in request.Meals)
        {
            var command = new CreateMealCommand(meal.ClientGuid, meal.MealTime, meal.ConsumedAt, meal.ClientCreatedAt, meal.Items);
            await ProcessItemAsync(
                meal.ClientGuid,
                nameof(Meal),
                async ct => (await _mediator.Send(command, ct).ConfigureAwait(false)).MealId,
                ct => _mealRepository.FindByClientGuidAsync(meal.ClientGuid, ct),
                meal => meal.Id,
                accepted, duplicates, errors,
                onAccepted: () => mealsAccepted++,
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var symptom in request.Symptoms)
        {
            var command = new CreateSymptomCommand(symptom.ClientGuid, symptom.SymptomType, symptom.Intensity, symptom.OccurredAt, symptom.ClientCreatedAt);
            await ProcessItemAsync(
                symptom.ClientGuid,
                nameof(Symptom),
                async ct => (await _mediator.Send(command, ct).ConfigureAwait(false)).SymptomId,
                ct => _symptomRepository.FindByClientGuidAsync(symptom.ClientGuid, ct),
                existing => existing.Id,
                accepted, duplicates, errors,
                onAccepted: () => symptomsAccepted++,
                cancellationToken).ConfigureAwait(false);
        }

        var additionalContext = JsonSerializer.Serialize(new { mealsAccepted, symptomsAccepted });

        // Auditoría del resumen del lote: cada comida y síntoma ya persistió en su subcomando; este
        // SaveChanges persiste solo la fila resumen de la bitácora, que el AuditLogger enrola sin
        // persistir por su cuenta (DEC-B5-01 capa 3, acta A8).
        await _auditLogger.LogAsync(
            AuditActionType.Create, "SyncBatch", entityId: null,
            oldValuesHash: null, newValuesHash: null, additionalContext, cancellationToken: cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Sync batch processed: {Accepted} accepted, {Duplicates} duplicates, {Errors} errors.",
            accepted.Count, duplicates.Count, errors.Count);

        return new SyncBatchResult(accepted, duplicates, errors);
    }

    private async Task ProcessItemAsync<TEntity>(
        Guid clientGuid,
        string entityType,
        Func<CancellationToken, Task<Guid>> sendAsync,
        Func<CancellationToken, Task<TEntity?>> findExistingAsync,
        Func<TEntity, Guid> selectId,
        List<SyncAcceptedEntry> accepted,
        List<SyncDuplicateEntry> duplicates,
        List<SyncErrorEntry> errors,
        Action onAccepted,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        try
        {
            var serverId = await sendAsync(cancellationToken).ConfigureAwait(false);
            if (_idempotencyContext.WasReplay)
            {
                duplicates.Add(new SyncDuplicateEntry(clientGuid, serverId));
            }
            else
            {
                accepted.Add(new SyncAcceptedEntry(clientGuid, serverId, entityType));
                onAccepted();
            }
        }
        catch (IdempotencyMismatchException exception)
        {
            errors.Add(new SyncErrorEntry(clientGuid, "idempotency_mismatch", exception.Message));
        }
        catch (ValidationException exception)
        {
            errors.Add(new SyncErrorEntry(clientGuid, "validation_error", exception.Message));
        }
        catch (DomainException exception)
        {
            errors.Add(new SyncErrorEntry(clientGuid, ResolveErrorCode(exception), exception.Message));
        }
        catch (ArgumentException exception)
        {
            errors.Add(new SyncErrorEntry(clientGuid, "invalid_request", exception.Message));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Caso degradado: KeyDB no disponible y la restricción única de client_guid
            // rechazó el duplicado. Se descarta el rastreo y se resuelve consultando el
            // registro existente.
            _unitOfWork.DiscardTrackedChanges();
            var existing = await findExistingAsync(cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                duplicates.Add(new SyncDuplicateEntry(clientGuid, selectId(existing)));
            }
            else
            {
                _logger.LogError(exception, "Unexpected error processing sync batch item for {EntityType}.", entityType);
                errors.Add(new SyncErrorEntry(clientGuid, "persistence_error", "No se pudo procesar el registro."));
            }
        }
    }

    private static string ResolveErrorCode(DomainException exception)
    {
        return exception switch
        {
            FoodItemNotFoundException => "food_item_not_found",
            CustomFoodNotFoundException => "custom_food_not_found",
            PatientResourceAccessException => "patient_resource_access_denied",
            InvalidMealRegistrationException => "invalid_meal_registration",
            _ => "domain_rule_violation"
        };
    }
}
