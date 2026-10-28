namespace Kable.Tests.Cases.Transports.Lifecycle;

using System.IO.Pipelines;
using Kable.Transports.Lifecycle;
using NSubstitute;
using Xunit;

public sealed class StreamConnectionLifetimeTests
{
    [Fact]
    public async Task Cleanup_attempts_every_resource_and_shares_the_failure_between_disposers()
    {
        var input = Substitute.For<PipeReader>();
        var output = Substitute.For<PipeWriter>();
        input.CompleteAsync(Arg.Any<Exception?>()).Returns(_ => new ValueTask(Task.FromException(new IOException("reader"))));
        output.CompleteAsync(Arg.Any<Exception?>()).Returns(_ => new ValueTask(Task.FromException(new IOException("writer"))));
        using var stream = new MemoryStream();
        var closed = 0;
        var lifetime = new StreamConnectionLifetime(stream, input, output, () => closed++);
        using var registration = lifetime.Closed.Register(() => throw new InvalidOperationException("cancel callback"));
        var first = lifetime.DisposeAsync().AsTask();
        var second = lifetime.DisposeAsync().AsTask();
        Assert.Same(first, second);
        var error = await Assert.ThrowsAsync<AggregateException>(() => first);
        Assert.Equal(3, error.InnerExceptions.Count);
        Assert.Equal(1, closed);
        Assert.False(stream.CanWrite);
        Assert.True(lifetime.Closed.IsCancellationRequested);
        await input.Received(1).CompleteAsync(Arg.Any<Exception?>());
        await output.Received(1).CompleteAsync(Arg.Any<Exception?>());
    }
}
