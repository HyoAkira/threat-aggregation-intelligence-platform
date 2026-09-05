using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using ThreatIntelligence.Application.Behaviors;

namespace ThreatIntelligence.Application.Tests.Behaviors;

public class ExceptionHandlingBehaviorTests
{
    private record TestRequest : IRequest<Unit>;

    [Fact]
    public async Task Handle_WhenNextSucceeds_ReturnsResult()
    {
        var behavior = new ExceptionHandlingBehavior<TestRequest, Unit>(NullLogger<ExceptionHandlingBehavior<TestRequest, Unit>>.Instance);
        Task<Unit> Next(CancellationToken _) => Task.FromResult(Unit.Value);

        var result = await behavior.Handle(new TestRequest(), Next, CancellationToken.None);

        Assert.Equal(Unit.Value, result);
    }

    [Fact]
    public async Task Handle_WhenNextThrows_RethrowsOriginalException()
    {
        var behavior = new ExceptionHandlingBehavior<TestRequest, Unit>(NullLogger<ExceptionHandlingBehavior<TestRequest, Unit>>.Instance);
        var original = new InvalidOperationException("ThreatFox API key (AuthKey:AbuseCh) is not configured.");
        Task<Unit> Next(CancellationToken _) => Task.FromException<Unit>(original);

        var rethrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => behavior.Handle(new TestRequest(), Next, CancellationToken.None));

        Assert.Same(original, rethrown);
    }
}