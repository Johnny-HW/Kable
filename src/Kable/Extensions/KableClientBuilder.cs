namespace Kable.Extensions;

using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Configuration;
using Kable.Core;
using Kable.Engine;
using Kable.Observability;
using Kable.Transports;
using Kable.Transports.Simulators;

public sealed class KableClientBuilder<TMessage>
{
    private IConnectionFactory? _factory;
    private IProtocolCodec<TMessage>? _codec;
    private ICommObserver? _observer;
    private string _deviceId = "DEFAULT";

    /// <summary>
    /// KableDeviceOptions 설정 객체를 주입하여 전송 계층 및 장비 ID를 일괄 바인딩합니다.
    /// </summary>
    public KableClientBuilder<TMessage> UseOptions(KableDeviceOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        _deviceId = options.DeviceId ?? "DEFAULT";

        switch (options.Transport?.Trim().ToLowerInvariant())
        {
            case "tcp":
            case "socket":
                UseTcp(options.Host, options.Port);
                break;

            case "serial":
            case "serialport":
            case "rs232":
            case "rs485":
                UseSerialPort(
                    options.PortName,
                    options.BaudRate,
                    options.GetParity(),
                    options.DataBits,
                    options.GetStopBits());
                break;

            case "namedpipe":
            case "pipe":
            case "ipc":
                UseNamedPipe(options.PipeName, options.ServerName, options.TimeoutMs);
                break;

            case "simulator":
            case "mock":
                UseSimulator(sim => { });
                break;

            default:
                throw new NotSupportedException($"지원되지 않는 Transport 유형입니다: '{options.Transport}'. (Tcp, Serial, NamedPipe, Simulator 지원)");
        }

        return this;
    }

    /// <summary>
    /// KableDeviceOptions 설정 객체를 주입합니다. (UseOptions의 별칭)
    /// </summary>
    public KableClientBuilder<TMessage> WithOptions(KableDeviceOptions options) => UseOptions(options);

    public KableClientBuilder<TMessage> UseDeviceId(string deviceId)
    {
        _deviceId = deviceId ?? "DEFAULT";
        return this;
    }

    public KableClientBuilder<TMessage> WithDeviceId(string deviceId)
    {
        _deviceId = deviceId ?? "DEFAULT";
        return this;
    }

    public KableClientBuilder<TMessage> UseTcp(string host, int port)
    {
        _factory = new TcpConnectionFactory(host, port);
        return this;
    }

    public KableClientBuilder<TMessage> UseSerialPort(
        string portName,
        int baudRate = 9600,
        Parity parity = Parity.None,
        int dataBits = 8,
        StopBits stopBits = StopBits.One)
    {
        _factory = new SerialPortConnectionFactory(portName, baudRate, parity, dataBits, stopBits);
        return this;
    }

    public KableClientBuilder<TMessage> UseNamedPipe(string pipeName, string serverName = ".", int timeoutMs = 5000)
    {
        _factory = new NamedPipeConnectionFactory(pipeName, serverName, timeoutMs);
        return this;
    }

    /// <summary>
    /// 실물 하드웨어 없이 인메모리 루프백 시뮬레이터로 통신을 모사합니다.
    /// </summary>
    public KableClientBuilder<TMessage> UseSimulator(Action<MockHardwareSimulator> configure)
    {
        var (clientContext, serverContext) = InMemoryConnectionContext.CreatePair();
        var simulator = new MockHardwareSimulator(serverContext);
        configure(simulator);
        simulator.Start();

        _factory = new DelegateConnectionFactory(() => new ValueTask<IConnectionContext>(clientContext));
        return this;
    }

    public KableClientBuilder<TMessage> UseConnectionFactory(IConnectionFactory factory)
    {
        _factory = factory;
        return this;
    }

    public KableClientBuilder<TMessage> UseCodec(IProtocolCodec<TMessage> codec)
    {
        _codec = codec;
        return this;
    }

    public KableClientBuilder<TMessage> UseObserver(ICommObserver observer)
    {
        _observer = observer;
        return this;
    }

    public KableClientBuilder<TMessage> WithObserver(ICommObserver observer)
    {
        _observer = observer;
        return this;
    }

    public IDeviceSession<TMessage> Build()
    {
        if (_factory == null)
            throw new InvalidOperationException("ConnectionFactory must be configured (e.g. UseTcp, UseSerialPort, or UseSimulator).");

        if (_codec == null)
            throw new InvalidOperationException("ProtocolCodec must be configured (e.g. UseCodec).");

        return new KableSession<TMessage>(_factory, _codec, _observer, deviceId: _deviceId);
    }

    private sealed class DelegateConnectionFactory : IConnectionFactory
    {
        private readonly Func<ValueTask<IConnectionContext>> _creator;
        public DelegateConnectionFactory(Func<ValueTask<IConnectionContext>> creator) => _creator = creator;
        public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default) => _creator();
    }
}
