namespace Kable.Tests.Cases;

using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Exceptions;
using Kable.Observability;
using Kable.Tests.Fixtures;
using NSubstitute;
using Xunit;

public sealed class SessionObservabilityTests
{
    private sealed class SignalingObserver : ICommObserver
    {
        private readonly Func<PacketTraceRecord, bool> _predicate;
        private readonly TaskCompletionSource<PacketTraceRecord> _matched =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SignalingObserver(Func<PacketTraceRecord, bool> predicate) => _predicate = predicate;

        public Task<PacketTraceRecord> Matched => _matched.Task;
        public System.Threading.Channels.ChannelReader<PacketTraceRecord> CommandStream => throw new NotSupportedException();
        public System.Threading.Channels.ChannelReader<PacketTraceRecord> PeriodicStream => throw new NotSupportedException();
        public System.Threading.Channels.ChannelReader<PacketTraceRecord> AlarmStream => throw new NotSupportedException();

        public void OnPacketTrace(in PacketTraceRecord record)
        {
            if (_predicate(record))
            {
                _matched.TrySetResult(record);
            }
        }
    }

    private sealed class FaultyAutonomousCodec : IProtocolCodec<string>
    {
        public bool SupportsCorrelationId => false;
        public string? ExtractCorrelationId(string message) => null;
        public void Encode(string message, IBufferWriter<byte> output) { }

        public bool TryDecode(ref ReadOnlySequence<byte> buffer, out string message)
        {
            if (buffer.Length > 0)
            {
                message = "FAULTY_MESSAGE";
                buffer = buffer.Slice(buffer.End);
                return true;
            }
            message = string.Empty;
            return false;
        }

        public bool IsAutonomousMessage(string message)
        {
            if (message == "FAULTY_MESSAGE")
            {
                throw new InvalidOperationException("Unexpected bug in autonomous message parser");
            }
            return false;
        }
    }

    [Fact]
    public async Task KableSession_WithCommObserver_RoutesAlarmsAndCommandsToObserver()
    {
        var alarmSeen = new TaskCompletionSource<PacketTraceRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandSeen = new TaskCompletionSource<PacketTraceRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new CommObserver();
        var mockObserver = Substitute.For<ICommObserver>();
        mockObserver.When(o => o.OnPacketTrace(Arg.Any<PacketTraceRecord>())).Do(call =>
        {
            var r = call.Arg<PacketTraceRecord>();
            if (r.Kind == TrafficKind.SpontaneousAlarm && r.ParsedText == "$ALARM_HIGH_TEMP") alarmSeen.TrySetResult(r);
            if (r.Kind == TrafficKind.AperiodicCommand && r.ParsedText == "STATUS_READY") commandSeen.TrySetResult(r);
        });
        _ = observer;

        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec, mockObserver);
        await session.StartAsync();

        await factory.Context.WriteAsciiLineAsync("$ALARM_HIGH_TEMP", 0x0A);
        await alarmSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var reqTask = session.RequestAsync<string>("GET_STATUS", TimeSpan.FromSeconds(2));
        await factory.Context.WriteAsciiLineAsync("STATUS_READY", 0x0A);
        await reqTask;

        await commandSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task TC_OBS_102_KableSession_LogLevelClassification_EmitsDebugForCommands_AndCriticalForUrgent()
    {
        var mockObserver = Substitute.For<ICommObserver>();
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec, mockObserver);
        await session.StartAsync();

        await session.SendAsync("TEST_DEBUG_SEND");
        mockObserver.Received().OnPacketTrace(Arg.Is<PacketTraceRecord>(r =>
            r.Level == LogLevel.Debug &&
            r.Tag == "SEND" &&
            r.ParsedText == "TEST_DEBUG_SEND"));

        await session.SendUrgentAsync("E_STOP_CRITICAL");
        mockObserver.Received().OnPacketTrace(Arg.Is<PacketTraceRecord>(r =>
            r.Level == LogLevel.Critical &&
            r.Tag == "URGENT_OOB" &&
            r.ParsedText == "E_STOP_CRITICAL"));
    }

    [Fact]
    public async Task TC_OBS_103_KableSession_TimeoutAndStreamCrash_EmitsWarningAndErrorLogLevels()
    {
        var mockObserver = Substitute.For<ICommObserver>();
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using (var session = new KableSession<string>(factory, codec, mockObserver))
        {
            await session.StartAsync();

            Func<Task> actTimeout = async () =>
                await session.RequestAsync<string>("SILENT_REQ", TimeSpan.FromMilliseconds(50));
            await actTimeout.Should().ThrowAsync<DeviceTimeoutException>();

            mockObserver.Received().OnPacketTrace(Arg.Is<PacketTraceRecord>(r =>
                r.Level == LogLevel.Warning &&
                r.Tag == "DEVICE_TIMEOUT" &&
                r.Kind == TrafficKind.SpontaneousAlarm));
        }

        var readFaultObserver = new SignalingObserver(r =>
            r.Level == LogLevel.Error && r.Tag == "READ_LOOP_FAULT" && r.Kind == TrafficKind.SpontaneousAlarm);
        var factory2 = new TestMemoryConnectionFactory();
        await using (var session2 = new KableSession<string>(factory2, codec, readFaultObserver))
        {
            await session2.StartAsync();
            factory2.Context.RemoteWrite.Complete(new IOException("Simulated physical pipe corruption"));
            await readFaultObserver.Matched.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task TC_OBS_104_KableSession_DispatchLoopException_LogsErrorAndDoesNotHangEngine()
    {
        var faultCodec = new FaultyAutonomousCodec();
        var dispatchFaultObserver = new SignalingObserver(r =>
            r.Level == LogLevel.Error &&
            r.Tag == "DISPATCH_MESSAGE_FAULT" &&
            r.ParsedText != null &&
            r.ParsedText.Contains("InvalidOperationException"));
        var factory = new TestMemoryConnectionFactory();
        await using var session = new KableSession<string>(factory, faultCodec, dispatchFaultObserver);
        await session.StartAsync();

        await factory.Context.RemoteWrite.WriteAsync(new byte[] { 0x01 });
        await factory.Context.RemoteWrite.FlushAsync();

        await dispatchFaultObserver.Matched.WaitAsync(TimeSpan.FromSeconds(5));
        await session.StopAsync();
    }

    [Fact]
    public async Task TC_OBS_202_KableSession_CleanShutdown_DoesNotEmitFalseErrorAlerts()
    {
        var mockObserver = Substitute.For<ICommObserver>();
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec, mockObserver);

        await session.StartAsync();
        await session.StopAsync();

        mockObserver.DidNotReceive().OnPacketTrace(Arg.Is<PacketTraceRecord>(r =>
            r.Level == LogLevel.Error ||
            r.Tag == "READ_LOOP_FAULT" ||
            r.Tag == "DISPATCH_LOOP_FAULT"));
    }

    [Fact]
    public async Task TC_OBS_203_KableSession_TxFlushFailure_EmitsErrorRecordAndPropagatesFailFast()
    {
        var mockObserver = Substitute.For<ICommObserver>();
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec, mockObserver);
        await session.StartAsync();

        factory.Context.RemoteRead.Complete(new IOException("Hardware cable detached during flush"));

        Func<Task> act = async () => await session.RequestAsync<string>("TEST_REQ", TimeSpan.FromSeconds(2));

        await act.Should().ThrowAsync<DeviceDisconnectedException>();

        mockObserver.Received().OnPacketTrace(Arg.Is<PacketTraceRecord>(r =>
            r.Level == LogLevel.Error &&
            r.Tag == "IO_FLUSH_ERROR" &&
            r.Kind == TrafficKind.SpontaneousAlarm));
    }
}
