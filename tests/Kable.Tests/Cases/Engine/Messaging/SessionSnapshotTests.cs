namespace Kable.Tests.Cases.Engine.Messaging;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;
using Kable.Codecs;
using Kable.Engine;
using Kable.Observability;
using Kable.Tests.Fixtures;
using Xunit;

public sealed class SessionSnapshotTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RawSnapshots_AreCreatedOnlyForEnabledObserver(bool correlation, bool observe)
    {
        var factory = new TestMemoryConnectionFactory();
        var request = new CountingMessage(1);
        var response = new CountingMessage(2);
        var observer = observe ? new CommObserver() : null;
        await using var session = new KableSession<CountingMessage>(factory,
            new CountingCodec(correlation, response), observer);
        await session.StartAsync();
        var pending = session.RequestAsync<CountingMessage>(request, TimeSpan.FromSeconds(5)).AsTask();
        var sent = await factory.Context.RemoteRead.ReadAsync();
        factory.Context.RemoteRead.AdvanceTo(sent.Buffer.End);
        await factory.Context.RemoteWrite.WriteAsync(new byte[] { 2 });
        Assert.Same(response, await pending);
        Assert.Equal(observe ? 1 : 0, request.Calls);
        Assert.Equal(observe ? 1 : 0, response.Calls);
        if (observer != null)
        {
            var traces = new List<PacketTraceRecord>();
            while (observer.CommandStream.TryRead(out var trace)) traces.Add(trace);
            var tx = Assert.Single(traces, t => t.Tag == "REQ_TX");
            var rx = Assert.Single(traces, t => t.Tag == "RESP_RX");
            Assert.Equal(PacketDirection.Tx, tx.Direction);
            Assert.Equal(PacketDirection.Rx, rx.Direction);
            Assert.Equal(new byte[] { 1 }, tx.RawBytes.ToArray());
            Assert.Equal(new byte[] { 2 }, rx.RawBytes.ToArray());
        }
    }

    private sealed class CountingMessage(byte value) : IRawByteProvider
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        public ReadOnlyMemory<byte> ToRawBytes()
        {
            Interlocked.Increment(ref _calls);
            return new byte[] { value };
        }
    }

    private sealed class CountingCodec(bool correlation, CountingMessage response) : IProtocolCodec<CountingMessage>
    {
        public bool SupportsCorrelationId => correlation;
        public string? ExtractCorrelationId(CountingMessage message) => correlation ? "1" : null;
        public bool IsAutonomousMessage(CountingMessage message) => false;
        public void Encode(CountingMessage message, IBufferWriter<byte> output)
        {
            output.GetSpan(1)[0] = 1;
            output.Advance(1);
        }
        public bool TryDecode(ref ReadOnlySequence<byte> buffer, out CountingMessage message)
        {
            message = response;
            if (buffer.IsEmpty) return false;
            buffer = buffer.Slice(1);
            return true;
        }
    }
}
