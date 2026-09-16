using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kable.Channels;
using Kable.Core;
using Xunit;

namespace Kable.Tests.Channels;

public sealed class MockTestConnectionContext : IConnectionContext
{
    private readonly Pipe _inputPipe = new();
    private readonly Pipe _outputPipe = new();
    private readonly CancellationTokenSource _closedCts = new();

    public string ConnectionId => "MockConnection";
    public string EndpointDescription => "mock://test";
    public PipeReader Input => _inputPipe.Reader;
    public PipeWriter Output => _outputPipe.Writer;
    public CancellationToken ConnectionClosed => _closedCts.Token;

    // Helper properties for test simulation
    public PipeWriter TestInputWriter => _inputPipe.Writer;
    public PipeReader TestOutputReader => _outputPipe.Reader;

    public bool IsDisposed { get; private set; }

    public void Abort(string reason)
    {
        _closedCts.Cancel();
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        _closedCts.Cancel();
        _inputPipe.Reader.Complete();
        _inputPipe.Writer.Complete();
        _outputPipe.Reader.Complete();
        _outputPipe.Writer.Complete();
        _closedCts.Dispose();
        return ValueTask.CompletedTask;
    }
}

public sealed class MockTestConnectionFactory : IConnectionFactory
{
    public MockTestConnectionContext Context { get; } = new();
    public int ConnectCount { get; private set; }

    public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        ConnectCount++;
        return ValueTask.FromResult<IConnectionContext>(Context);
    }
}

public class KableModbusDeviceChannelTests
{
    [Fact]
    public async Task OpenAndClose_ControlsConnectionLifecycle()
    {
        var factory = new MockTestConnectionFactory();
        await using var channel = new KableModbusDeviceChannel(factory, "TEST_UNIT");

        Assert.False(channel.IsOpen);

        await channel.OpenAsync();
        Assert.True(channel.IsOpen);
        Assert.Equal(1, factory.ConnectCount);

        await channel.CloseAsync();
        Assert.False(channel.IsOpen);
        Assert.True(factory.Context.IsDisposed);
    }

    [Fact]
    public async Task SendAndReceiveFrameAsync_WhenNotOpen_ThrowsInvalidOperationException()
    {
        var factory = new MockTestConnectionFactory();
        await using var channel = new KableModbusDeviceChannel(factory, "TEST_UNIT");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await channel.SendAndReceiveFrameAsync([0x01, 0x05, 0x00, 0x00, 0xFF, 0x00], TimeSpan.FromSeconds(1));
        });
    }

    [Fact]
    public async Task SendAndReceiveFrameAsync_EncodesPduAndReturnsDecodedPdu()
    {
        var factory = new MockTestConnectionFactory();
        await using var channel = new KableModbusDeviceChannel(factory, "TEST_UNIT");
        channel.Open();

        byte[] requestPdu = [0x01, 0x05, 0x00, 0x01, 0xFF, 0x00];

        // Background server simulation: read sent frame (with CRC) and echo response
        var serverTask = Task.Run(async () =>
        {
            var readResult = await factory.Context.TestOutputReader.ReadAsync();
            var buffer = readResult.Buffer;
            Assert.True(buffer.Length >= 8); // 6 bytes PDU + 2 bytes CRC
            factory.Context.TestOutputReader.AdvanceTo(buffer.End);

            // Server responds with echo + valid CRC:
            byte[] responseWithCrc = new byte[requestPdu.Length + 2];
            Kable.Core.Checksums.Crc16Modbus.Append(requestPdu, responseWithCrc);
            await factory.Context.TestInputWriter.WriteAsync(responseWithCrc);
            await factory.Context.TestInputWriter.FlushAsync();
        });

        var response = await channel.SendAndReceiveFrameAsync(requestPdu, TimeSpan.FromSeconds(2));

        byte[] expectedFullFrame = new byte[requestPdu.Length + 2];
        Kable.Core.Checksums.Crc16Modbus.Append(requestPdu, expectedFullFrame);
        Assert.Equal(expectedFullFrame, response);
        await serverTask;
    }
}
