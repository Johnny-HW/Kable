namespace Kable.Tests.Channels;

using System;
using System.Threading;
using System.Threading.Tasks;
using Kable.Channels;
using Kable.Exceptions;
using Xunit;

public sealed class ModbusChannelFailureTests
{
    private static readonly byte[] Request = [1, 5, 0, 1, 255, 0];

    [Fact]
    public async Task TimeoutAfterSend_ClosesConnection()
    {
        var factory = new MockTestConnectionFactory();
        await using var channel = new KableModbusDeviceChannel(factory);
        await channel.OpenAsync();
        await Assert.ThrowsAsync<TimeoutException>(() =>
            channel.SendAndReceiveFrameAsync(Request, TimeSpan.FromMilliseconds(50)).AsTask());
        Assert.False(channel.IsOpen);
        Assert.True(factory.Context.IsDisposed);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            channel.SendAndReceiveFrameAsync(Request, TimeSpan.FromSeconds(1)).AsTask());
    }

    [Fact]
    public async Task CancellationAfterSend_ClosesConnection()
    {
        var factory = new MockTestConnectionFactory();
        await using var channel = new KableModbusDeviceChannel(factory);
        await channel.OpenAsync();
        using var cts = new CancellationTokenSource();
        var pending = channel.SendAndReceiveFrameAsync(Request, Timeout.InfiniteTimeSpan, cts.Token).AsTask();
        var sent = await factory.Context.TestOutputReader.ReadAsync();
        factory.Context.TestOutputReader.AdvanceTo(sent.Buffer.End);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.False(channel.IsOpen);
    }

    [Fact]
    public async Task CancellationAndInvalidTimeoutBeforeSend_PreserveConnection()
    {
        var factory = new MockTestConnectionFactory();
        await using var channel = new KableModbusDeviceChannel(factory);
        await channel.OpenAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => channel.SendAndReceiveFrameAsync(
            Request, TimeSpan.FromSeconds(1), new CancellationToken(true)).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => channel.SendAndReceiveFrameAsync(
            Request, TimeSpan.FromMilliseconds(-2)).AsTask());
        Assert.True(channel.IsOpen);
    }

    [Fact]
    public async Task LockWaitTimeout_PreservesActiveRequest()
    {
        var factory = new MockTestConnectionFactory();
        await using var channel = new KableModbusDeviceChannel(factory);
        await channel.OpenAsync();
        using var cts = new CancellationTokenSource();
        var first = channel.SendAndReceiveFrameAsync(Request, Timeout.InfiniteTimeSpan, cts.Token).AsTask();
        var sent = await factory.Context.TestOutputReader.ReadAsync();
        factory.Context.TestOutputReader.AdvanceTo(sent.Buffer.End);
        try
        {
            var waiting = channel.SendAndReceiveFrameAsync(Request, TimeSpan.FromMilliseconds(50)).AsTask();
            await Assert.ThrowsAsync<TimeoutException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.True(waiting.IsCompleted);
            Assert.True(channel.IsOpen);
        }
        finally { cts.Cancel(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
    }

    [Fact]
    public async Task Close_CancelsActiveRequest_AndDisposeIsTerminal()
    {
        var factory = new MockTestConnectionFactory();
        var channel = new KableModbusDeviceChannel(factory);
        await Task.WhenAll(channel.OpenAsync(), channel.OpenAsync());
        Assert.Equal(1, factory.ConnectCount);
        var pending = channel.SendAndReceiveFrameAsync(Request, Timeout.InfiniteTimeSpan).AsTask();
        var sent = await factory.Context.TestOutputReader.ReadAsync();
        factory.Context.TestOutputReader.AdvanceTo(sent.Buffer.End);
        await channel.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<DeviceDisconnectedException>(() => pending);
        await Task.WhenAll(channel.DisposeAsync().AsTask(), channel.DisposeAsync().AsTask());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => channel.OpenAsync());
    }

    [Fact]
    public async Task InputCompletion_IsPhysicalDisconnect()
    {
        var factory = new MockTestConnectionFactory();
        await using var channel = new KableModbusDeviceChannel(factory);
        await channel.OpenAsync();
        var pending = channel.SendAndReceiveFrameAsync(Request, Timeout.InfiniteTimeSpan).AsTask();
        var sent = await factory.Context.TestOutputReader.ReadAsync();
        factory.Context.TestOutputReader.AdvanceTo(sent.Buffer.End);
        await factory.Context.TestInputWriter.CompleteAsync();
        await Assert.ThrowsAsync<DeviceDisconnectedException>(() => pending);
        Assert.False(channel.IsOpen);
    }
}
