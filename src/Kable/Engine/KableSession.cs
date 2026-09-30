namespace Kable.Engine;

using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Core;
using Kable.Observability;

public sealed partial class KableSession<TMessage> : IDeviceSession<TMessage>
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

    public string DeviceId { get; }
    public bool IsConnected => State == SessionLifecycleState.Running && Volatile.Read(ref _isConnected) == 1;

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
