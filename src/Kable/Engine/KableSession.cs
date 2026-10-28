namespace Kable.Engine;

using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Core;
using Kable.Observability;

public sealed partial class KableSession<TMessage> : IDeviceSession<TMessage>, IRequestTimeoutProvider
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly IProtocolCodec<TMessage> _codec;
    private readonly Decoding.FrameDecoder<TMessage> _decoder;
    private readonly ICommObserver? _observer;
    private readonly KableSessionOptions<TMessage> _sessionOptions;
    private readonly HeartbeatOptions<TMessage>? _heartbeatOptions;
    private readonly TimeProvider _timeProvider;

    private readonly Streaming.InboundDelivery<TMessage> _delivery;

    private readonly Alarms.AlarmSpoolWorker<TMessage> _alarmSpool;

    private IConnectionContext? _context;
    private Task? _readLoopTask;
    private Task? _deliveryTask;
    private Task? _outboundPumpTask;
    private Task? _heartbeatTask;
    private readonly CancellationTokenSource _sessionCts = new();
    private int _isConnected;
    private long _lastInboundTicks;
    private readonly CommHealthTracker _healthTracker = new();

    public string DeviceId { get; }
    public TimeSpan DefaultRequestTimeout { get; internal set; } = TimeSpan.FromSeconds(3);
    public bool IsConnected => State == SessionLifecycleState.Running && Volatile.Read(ref _isConnected) == 1;
    public CommHealthMetrics HealthMetrics => _healthTracker.GetMetricsSnapshot();

    /// <summary>
    /// 세션 종료 후 또는 실시간 알람 스풀 워커 처리 결과 집계 리포트 (저장 완료, 저장 여부 불명, 미처리)
    /// </summary>
    public AlarmSpoolSummary SpoolSummary => _alarmSpool.Summary;

    public KableSession(
        IConnectionFactory connectionFactory,
        IProtocolCodec<TMessage> codec,
        KableSessionOptions<TMessage> options)
        : this(connectionFactory, codec, null, null, "DEFAULT", options)
    {
    }

    public KableSession(
        IConnectionFactory connectionFactory,
        IProtocolCodec<TMessage> codec,
        ICommObserver? observer = null,
        HeartbeatOptions<TMessage>? heartbeatOptions = null,
        string deviceId = "DEFAULT",
        KableSessionOptions<TMessage>? sessionOptions = null)
    {
        _connectionFactory = connectionFactory;
        _codec = codec;
        _responses = new Messaging.ResponseRouter<TMessage>(codec);
        _decoder = new Decoding.FrameDecoder<TMessage>(codec);
        _observer = observer;
        _heartbeatOptions = heartbeatOptions;
        _timeProvider = heartbeatOptions?.TimeProvider ?? TimeProvider.System;
        DeviceId = deviceId ?? "DEFAULT";
        _sessionOptions = (sessionOptions ?? new KableSessionOptions<TMessage>()).Snapshot();

        // 알람 채널: 상한(AlarmQueueCapacity) 및 포화 정책 적용 (무제한 메모리 증가 방지)
        var alarmOptions = new BoundedChannelOptions(_sessionOptions.AlarmQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = false
        };
        _alarmStream = Channel.CreateBounded<TMessage>(alarmOptions);

        _alarmSpool = new Alarms.AlarmSpoolWorker<TMessage>(_sessionOptions, _observer,
            error => _ = Task.Run(() => PerformCleanupAsync(error)));

        _delivery = new Streaming.InboundDelivery<TMessage>(_sessionOptions.InboundQueueCapacity,
            _sessionOptions.InboundDeliveryCapacity, _sessionOptions.TelemetryOverflowMode, message =>
                _observer?.OnPacketTrace(new PacketTraceRecord(DateTime.UtcNow, PacketDirection.Rx,
                    TrafficKind.PeriodicTelemetry, "TELEMETRY_DROPPED", ReadOnlyMemory<byte>.Empty,
                    message?.ToString(), TimeSpan.Zero, LogLevel.Warning)));
    }
}
