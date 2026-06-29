using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cauce.Application.Common.Behaviors;
using Cauce.Application.Common.Exceptions;
using Cauce.Application.Common.Idempotency;
using Cauce.Application.Common.Interfaces;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas del comportamiento <see cref="IdempotencyBehavior{TRequest,TResponse}"/>.
/// </summary>
public sealed class IdempotencyBehaviorTests
{
    private readonly IIdempotencyStore _store = Substitute.For<IIdempotencyStore>();
    private readonly IIdempotencyContext _idempotencyContext = Substitute.For<IIdempotencyContext>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ILogger<IdempotencyBehavior<TestCommand, TestResponse>> _logger =
        Substitute.For<ILogger<IdempotencyBehavior<TestCommand, TestResponse>>>();

    private readonly TestCommand _command = new(Guid.NewGuid(), "payload");
    private readonly TestResponse _freshResponse = new(Guid.NewGuid());

    public IdempotencyBehaviorTests()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());
    }

    private IdempotencyBehavior<TestCommand, TestResponse> CreateBehavior() =>
        new(_store, _idempotencyContext, _currentUserService, _logger);

    [Fact]
    public async Task Handle_CacheMiss_ExecutesHandlerAndPersists()
    {
        _store.GetAsync<IdempotencyRecord<TestResponse>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IdempotencyRecord<TestResponse>?)null);
        var nextCalled = false;
        RequestHandlerDelegate<TestResponse> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(_freshResponse);
        };

        var result = await CreateBehavior().Handle(_command, next, CancellationToken.None);

        nextCalled.Should().BeTrue();
        result.Should().Be(_freshResponse);
        _idempotencyContext.WasReplay.Should().BeFalse();
        await _store.Received(1).SetAsync(
            Arg.Any<string>(), Arg.Any<IdempotencyRecord<TestResponse>>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CacheHitMatchingHash_ReturnsCachedWithoutExecuting()
    {
        var cached = new TestResponse(Guid.NewGuid());
        _store.GetAsync<IdempotencyRecord<TestResponse>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new IdempotencyRecord<TestResponse>(ComputeHash(_command), cached));
        var nextCalled = false;
        RequestHandlerDelegate<TestResponse> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(_freshResponse);
        };

        var result = await CreateBehavior().Handle(_command, next, CancellationToken.None);

        nextCalled.Should().BeFalse();
        result.Should().Be(cached);
        _idempotencyContext.WasReplay.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CacheHitDifferentHash_ThrowsMismatch()
    {
        _store.GetAsync<IdempotencyRecord<TestResponse>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new IdempotencyRecord<TestResponse>("DIFFERENT_HASH", new TestResponse(Guid.NewGuid())));
        RequestHandlerDelegate<TestResponse> next = () => Task.FromResult(_freshResponse);

        var act = () => CreateBehavior().Handle(_command, next, CancellationToken.None);

        await act.Should().ThrowAsync<IdempotencyMismatchException>();
    }

    private static string ComputeHash(object request)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(request, request.GetType(), options);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    /// <summary>
    /// Comando idempotente de prueba.
    /// </summary>
    /// <param name="ClientGuid">Clave de idempotencia.</param>
    /// <param name="Data">Carga de ejemplo.</param>
    public sealed record TestCommand(Guid ClientGuid, string Data) : IRequest<TestResponse>, IIdempotentCommand;

    /// <summary>
    /// Respuesta de prueba.
    /// </summary>
    /// <param name="Id">Identificador de ejemplo.</param>
    public sealed record TestResponse(Guid Id);
}
