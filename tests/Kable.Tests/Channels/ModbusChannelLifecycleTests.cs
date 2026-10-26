namespace Kable.Tests.Channels;

using System;
using System.Threading;
using System.Threading.Tasks;
using Kable.Channels;
using Kable.Core;
using Kable.Core.Checksums;
using Xunit;

public sealed class ModbusChannelLifecycleTests
{
    private static readonly byte[] Request = [1, 5, 0, 1, 255, 0];

    [Fact]
    public async Task FailedConnection_IsNotReusedAfterExplicitOpen()
    {
        var factory = new FreshFactory();
        await using var channel = new KableModbusDeviceChannel(factory);
        await channel.OpenAsync();
        var old = factory.Last!;
        await Assert.ThrowsAsync<TimeoutException>(() => channel.SendAndReceiveFrameAsync(
            Request, TimeSpan.FromMilliseconds(100)).AsTask());
        await channel.OpenAsync();
        Assert.NotSame(old, factory.Last);
        Assert.Equal(1, old.DisposeCount);
        var pending = channel.SendAndReceiveFrameAsync(Request, TimeSpan.FromSeconds(5)).AsTask();
        var sent = await factory.Last!.TestOutputReader.ReadAsync();
        factory.Last.TestOutputReader.AdvanceTo(sent.Buffer.End);
        byte[] response = new byte[8];
        Crc16Modbus.Append(Request, response);
        await factory.Last.TestInputWriter.WriteAsync(response);
        Assert.Equal(response, await pending);
        Assert.True(channel.IsOpen);
    }

    [Fact]
    public async Task CleanupFailure_DoesNotReplaceRequestTimeout()
    {
        var factory = new MockTestConnectionFactory();
        await using var channel = new KableModbusDeviceChannel(factory);
        await channel.OpenAsync();
        factory.Context.FailCleanup = true;
        await Assert.ThrowsAsync<TimeoutException>(() => channel.SendAndReceiveFrameAsync(
            Request, TimeSpan.FromMilliseconds(100)).AsTask());
        Assert.False(channel.IsOpen);
        Assert.Equal(1, factory.Context.DisposeCount);
    }

    [Fact]
    public async Task DisposeDuringConnect_CancelsConnection_AndIsShared()
    {
        var factory = new DelayedFactory();
        var channel = new KableModbusDeviceChannel(factory);
        var opening = channel.OpenAsync();
        await factory.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var first = channel.DisposeAsync().AsTask();
        var second = channel.DisposeAsync().AsTask();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(channel.IsOpen);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => channel.OpenAsync());
    }

    [Fact]
    public async Task ConcurrentCloseAndDispose_DisposeConnectionOnce()
    {
        var factory = new MockTestConnectionFactory();
        var channel = new KableModbusDeviceChannel(factory);
        await channel.OpenAsync();
        await Task.WhenAll(channel.CloseAsync(), channel.DisposeAsync().AsTask(), channel.DisposeAsync().AsTask());
        Assert.Equal(1, factory.Context.DisposeCount);
    }

    private sealed class FreshFactory : IConnectionFactory
    {
        public MockTestConnectionContext? Last { get; private set; }
        public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Last = new MockTestConnectionContext();
            return new ValueTask<IConnectionContext>(Last);
        }
    }

    private sealed class DelayedFactory : IConnectionFactory
    {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
        {
            Entered.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("Unreachable.");
        }
    }
}
