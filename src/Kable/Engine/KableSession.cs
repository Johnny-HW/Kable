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
    private readonly ICommObserver? _observer;
    private readonly KableSessionOptions<TMessage> _sessionOptions;
    private readonly HeartbeatOptions<TMessage>? _heartbeatOptions;

    // Inbound Dispatch Queue
    private readonly Channel<TMessage> _dispatchQueue = Channel.CreateBounded<TMessage>(new BoundedChannelOptions(10000)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleWriter = true,
        SingleReader = true
    });

    private readonly Channel<TMessage>? _alarmSpoolQueue;

    private IConnectionContext? _context;
    private Task? _readLoopTask;
    private Task? _dispatchLoopTask;
    private Task? _outboundPumpTask;
    private Task? _alarmSpoolWorkerTask;
    private Task? _heartbeatTask;
    private readonly CancellationTokenSource _sessionCts = new();
    private int _isConnected;
    private long _lastInboundTicks;
    private long _spoolStoredCount;
    private long _spoolInDoubtCount;
    private long _spoolUnprocessedCount;

    public string DeviceId { get; }
    public TimeSpan DefaultRequestTimeout { get; internal set; } = TimeSpan.FromSeconds(3);
    public bool IsConnected => State == SessionLifecycleState.Running && Volatile.Read(ref _isConnected) == 1;

    /// <summary>
    /// 세션 종료 후 또는 실시간 알람 스풀 워커 처리 결과 집계 리포트 (저장 완료, 저장 여부 불명, 미처리)
    /// </summary>
    public AlarmSpoolSummary SpoolSummary => new(
        Volatile.Read(ref _spoolStoredCount),
        Volatile.Read(ref _spoolInDoubtCount),
        Volatile.Read(ref _spoolUnprocessedCount));

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
        _observer = observer;
        _heartbeatOptions = heartbeatOptions;
        DeviceId = deviceId ?? "DEFAULT";
        _sessionOptions = sessionOptions ?? new KableSessionOptions<TMessage>();

        // 알람 채널: 상한(AlarmQueueCapacity) 및 포화 정책 적용 (무제한 메모리 증가 방지)
        var alarmOptions = new BoundedChannelOptions(_sessionOptions.AlarmQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = false
        };
        _alarmStream = Channel.CreateBounded<TMessage>(alarmOptions);

        // 알람 스풀 큐: SpoolToStorage 모드일 때 외부 저장소 비동기 저장 워커용 유한 큐
        if (_sessionOptions.AlarmOverflowMode == AlarmOverflowMode.SpoolToStorage)
        {
            if (_sessionOptions.OnAlarmOverflowAsync == null)
            {
                throw new InvalidOperationException(
                    "KableSession configuration error: AlarmOverflowMode is set to SpoolToStorage, but OnAlarmOverflowAsync callback is not provided.");
            }

            var spoolOptions = new BoundedChannelOptions(_sessionOptions.AlarmSpoolQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = true
            };
            _alarmSpoolQueue = Channel.CreateBounded<TMessage>(spoolOptions);
        }

        // 텔레메트리 채널: 상한(InboundQueueCapacity) 및 오버플로우 정책 적용 (알람과 격리)
        var incomingOptions = new BoundedChannelOptions(_sessionOptions.InboundQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = false
        };
        _telemetryStream = Channel.CreateBounded<TMessage>(incomingOptions);
    }
}
