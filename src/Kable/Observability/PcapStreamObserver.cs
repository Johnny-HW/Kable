namespace Kable.Observability;

using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Kable.Core;

/// <summary>Nonblocking, bounded PCAP recording with a single ordered writer.</summary>
public sealed partial class PcapStreamObserver : ICommObserver, IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly PcapWriter _writer;
    private readonly Channel<PcapQueuedRecord> _recording;
    private readonly Channel<PacketTraceRecord> _commands;
    private readonly Channel<PacketTraceRecord> _periodic;
    private readonly Channel<PacketTraceRecord> _alarms;
    private readonly Task _worker;
    private Exception? _lastError;
    private bool _stopping;
    private Task? _disposeTask;
    private long _droppedCommands, _droppedPeriodic, _droppedAlarms;

    public ChannelReader<PacketTraceRecord> CommandStream => _commands.Reader;
    public ChannelReader<PacketTraceRecord> PeriodicStream => _periodic.Reader;
    public ChannelReader<PacketTraceRecord> AlarmStream => _alarms.Reader;
    public Task Completion => _worker;
    public Exception? LastError => Volatile.Read(ref _lastError);
    public long DroppedCommandCount => Interlocked.Read(ref _droppedCommands);
    public long DroppedPeriodicCount => Interlocked.Read(ref _droppedPeriodic);
    public long DroppedAlarmCount => Interlocked.Read(ref _droppedAlarms);

    public PcapStreamObserver(string filePath) : this(filePath, new PcapStreamObserverOptions()) { }
    public PcapStreamObserver(string filePath, PcapStreamObserverOptions options)
        : this(OpenFile(filePath, options), options) { }
    internal PcapStreamObserver(Stream stream, PcapStreamObserverOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.Validate();
        _writer = new PcapWriter(stream);
        _recording = Channel.CreateBounded<PcapQueuedRecord>(new BoundedChannelOptions(options.RecordingCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait, SingleReader = true, AllowSynchronousContinuations = false
        });
        _commands = View(options.CommandCapacity, () => Interlocked.Increment(ref _droppedCommands));
        _periodic = View(options.PeriodicCapacity, () => Interlocked.Increment(ref _droppedPeriodic));
        _alarms = View(options.AlarmCapacity, () => Interlocked.Increment(ref _droppedAlarms));
        _worker = Task.Run(RecordAsync);
    }
    private static Stream OpenFile(string path, PcapStreamObserverOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.Validate();
        return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
    }
    private static Channel<PacketTraceRecord> View(int capacity, Action dropped) =>
        Channel.CreateBounded<PacketTraceRecord>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest, AllowSynchronousContinuations = false
        }, _ => dropped());

    public void OnPacketTrace(in PacketTraceRecord trace)
    {
        lock (_sync)
        {
            if (_stopping) return;
            var payload = trace.RawBytes.Slice(0, Math.Min(trace.RawBytes.Length, 65535)).ToArray();
            var owned = new PacketTraceRecord(trace.TimestampUtc, trace.Direction, trace.Kind, trace.Tag,
                payload, trace.ParsedText, trace.Latency, trace.Level, trace.DeviceId, trace.SequenceNo);
            if (!_recording.Writer.TryWrite(new PcapQueuedRecord(payload, trace.RawBytes.Length, trace.TimestampUtc)))
            {
                FailLocked(new IOException("PCAP recording queue capacity exceeded; recording stopped."));
                return;
            }
            switch (trace.Kind)
            {
                case TrafficKind.PeriodicTelemetry: _periodic.Writer.TryWrite(owned); break;
                case TrafficKind.SpontaneousAlarm: _alarms.Writer.TryWrite(owned); break;
                default: _commands.Writer.TryWrite(owned); break;
            }
        }
    }
}
