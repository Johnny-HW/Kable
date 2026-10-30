namespace Kable.Tests.Cases.Engine.Messaging;

using System.Buffers;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Exceptions;
using Kable.Observability;
using Kable.Transports;
using Xunit;

public sealed class AuditSessionRegressionTests
{
    [Fact]
    public async Task DisconnectDoesNotReportRunningOrAllowRestart()
    {
        var (connection, peer) = InMemoryConnectionContext.CreatePair();
        await using var peerScope = peer;
        await using var session = new KableSession<string>(new Factory(connection), new AsciiLineCodec());
        await session.StartAsync();
        connection.Abort("test");
        Assert.False(session.IsConnected);
        Assert.NotEqual(SessionLifecycleState.Running, session.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync().AsTask());
    }

    [Fact]
    public async Task SendTraceIncludesSessionDeviceId()
    {
        var (connection, peer) = InMemoryConnectionContext.CreatePair();
        await using var peerScope = peer;
        var observer = new Capture();
        await using var session = new KableSession<string>(new Factory(connection), new AsciiLineCodec(), observer, deviceId: "PLC-A");
        await session.StartAsync();
        await session.SendAsync("PING");
        Assert.Contains(observer.Records, r => r.Tag == "SEND" && r.DeviceId == "PLC-A");
    }

    [Fact]
    public async Task ObserverCannotReplaceCidTimeout()
    {
        var (connection, peer) = InMemoryConnectionContext.CreatePair();
        await using var peerScope = peer;
        var observer = new Capture { ThrowTimeout = true };
        await using var session = new KableSession<string>(new Factory(connection), new CidCodec(), observer);
        await session.StartAsync();
        await Assert.ThrowsAsync<DeviceTimeoutException>(() => session.RequestAsync<string>("1:QUERY", TimeSpan.FromMilliseconds(100)).AsTask());
        Assert.True(session.IsConnected);
    }

    private sealed class Capture : ICommObserver
    {
        public ConcurrentQueue<PacketTraceRecord> Records { get; } = new();
        public bool ThrowTimeout { get; init; }
        private readonly Channel<PacketTraceRecord> _channel = Channel.CreateUnbounded<PacketTraceRecord>();
        public ChannelReader<PacketTraceRecord> PeriodicStream => _channel.Reader;
        public ChannelReader<PacketTraceRecord> CommandStream => _channel.Reader;
        public ChannelReader<PacketTraceRecord> AlarmStream => _channel.Reader;
        public void OnPacketTrace(in PacketTraceRecord trace)
        {
            if (ThrowTimeout && trace.Tag == "DEVICE_TIMEOUT") throw new InvalidOperationException("observer failure");
            Records.Enqueue(trace);
        }
    }
    private sealed class CidCodec : IProtocolCodec<string>
    {
        private readonly AsciiLineCodec _inner = new();
        public bool SupportsCorrelationId => true;
        public bool TryDecode(ref ReadOnlySequence<byte> buffer, out string message) => _inner.TryDecode(ref buffer, out message);
        public void Encode(string message, IBufferWriter<byte> output) => _inner.Encode(message, output);
        public string? ExtractCorrelationId(string message) => message.Split(':')[0];
        public bool IsAutonomousMessage(string message) => false;
    }
    private sealed class Factory(IConnectionContext context) : IConnectionFactory
    {
        public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default) => new(context);
    }
}
