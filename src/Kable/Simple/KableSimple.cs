namespace Kable.Simple;

using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Extensions;
using Kable.Observability;

/// <summary>
/// Pipelines와 반응형 스트림 학습 곡선 없이 즉시 장비와 통신할 수 있는 심플 파사드.
/// 동기 블로킹(.Result / .Wait())을 엄격히 배제하고 async/await 기반의 안전한 비차단 API를 제공합니다.
/// <para>
/// <b>리소스 해제 권장사항:</b> 동기 블로킹 방지를 위해 항상 <c>await using</c> 패턴을 사용하십시오.
/// <code>
/// await using var client = await KableSimple.OpenTcpAsync("192.168.0.100", 9000);
/// string response = await client.QueryAsync("IDN?");
/// </code>
/// </para>
/// </summary>
public static partial class KableSimple
{
    /// <summary>
    /// KableSimpleOptions 구성을 바탕으로 시리얼(RS-232/422/485) 포트를 개방하고 클라이언트를 시작합니다.
    /// </summary>
    public static async ValueTask<IKableSimpleClient> OpenSerialAsync(
        string portName,
        int baudRate,
        Parity parity,
        int dataBits,
        StopBits stopBits,
        KableSimpleOptions options,
        CancellationToken ct = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.Validate();
        var codec = new AsciiLineCodec(options.Delimiter, options.Encoding, isAutonomousPredicate: options.IsAutonomousMessage);
        var builder = new KableClientBuilder<string>()
            .UseSerialPort(portName, baudRate, parity, dataBits, stopBits)
            .UseCodec(codec);

        if (options.Observer != null)
        {
            builder.UseObserver(options.Observer);
        }

        var session = builder.Build();
        var client = new KableSimpleClient(session, options.Observer, options.DefaultTimeout);
        await client.InitializeAsync(ct).ConfigureAwait(false);
        return client;
    }

    /// <summary>
    /// 시리얼(RS-232/422/485) 포트를 개방하고 KableSimple 클라이언트를 시작합니다.
    /// </summary>
    public static ValueTask<IKableSimpleClient> OpenSerialAsync(
        string portName,
        int baudRate = 9600,
        Parity parity = Parity.None,
        int dataBits = 8,
        StopBits stopBits = StopBits.One,
        byte delimiter = 0x0A,
        Encoding? encoding = null,
        ICommObserver? observer = null,
        CancellationToken ct = default)
    {
        return OpenSerialAsync(portName, baudRate, parity, dataBits, stopBits, new KableSimpleOptions
        {
            Delimiter = delimiter,
            Encoding = encoding,
            Observer = observer
        }, ct);
    }

    /// <summary>
    /// KableSimpleOptions 구성을 바탕으로 TCP/IP 소켓을 연결하고 클라이언트를 시작합니다.
    /// </summary>
    public static async ValueTask<IKableSimpleClient> OpenTcpAsync(
        string host,
        int port,
        KableSimpleOptions options,
        CancellationToken ct = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.Validate();
        var codec = new AsciiLineCodec(options.Delimiter, options.Encoding, isAutonomousPredicate: options.IsAutonomousMessage);
        var builder = new KableClientBuilder<string>()
            .UseTcp(host, port, options.ConnectTimeoutMs)
            .UseCodec(codec);

        if (options.Observer != null)
        {
            builder.UseObserver(options.Observer);
        }

        var session = builder.Build();
        var client = new KableSimpleClient(session, options.Observer, options.DefaultTimeout);
        await client.InitializeAsync(ct).ConfigureAwait(false);
        return client;
    }

    /// <summary>
    /// TCP/IP 소켓을 연결하고 KableSimple 클라이언트를 시작합니다.
    /// </summary>
    public static ValueTask<IKableSimpleClient> OpenTcpAsync(
        string host,
        int port,
        byte delimiter = 0x0A,
        Encoding? encoding = null,
        ICommObserver? observer = null,
        CancellationToken ct = default)
    {
        return OpenTcpAsync(host, port, new KableSimpleOptions
        {
            Delimiter = delimiter,
            Encoding = encoding,
            Observer = observer
        }, ct);
    }

    /// <summary>
    /// KableSimpleOptions 구성을 바탕으로 Named Pipe를 연결하고 클라이언트를 시작합니다.
    /// </summary>
    public static async ValueTask<IKableSimpleClient> OpenNamedPipeAsync(
        string pipeName,
        string serverName,
        int timeoutMs,
        KableSimpleOptions options,
        CancellationToken ct = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.Validate();
        var codec = new AsciiLineCodec(options.Delimiter, options.Encoding, isAutonomousPredicate: options.IsAutonomousMessage);
        var builder = new KableClientBuilder<string>()
            .UseNamedPipe(pipeName, serverName, timeoutMs)
            .UseCodec(codec);

        if (options.Observer != null)
        {
            builder.UseObserver(options.Observer);
        }

        var session = builder.Build();
        var client = new KableSimpleClient(session, options.Observer, options.DefaultTimeout);
        await client.InitializeAsync(ct).ConfigureAwait(false);
        return client;
    }

    /// <summary>
    /// 로컬 고속 IPC인 Named Pipe를 연결하고 KableSimple 클라이언트를 시작합니다.
    /// </summary>
    public static ValueTask<IKableSimpleClient> OpenNamedPipeAsync(
        string pipeName,
        string serverName = ".",
        int timeoutMs = 5000,
        byte delimiter = 0x0A,
        Encoding? encoding = null,
        ICommObserver? observer = null,
        CancellationToken ct = default)
    {
        return OpenNamedPipeAsync(pipeName, serverName, timeoutMs, new KableSimpleOptions
        {
            Delimiter = delimiter,
            Encoding = encoding,
            Observer = observer
        }, ct);
    }

    /// <summary>
    /// 기존 구성된 IDeviceSession&lt;string&gt;을 심플 클라이언트 래퍼로 감싸 시작합니다.
    /// </summary>
    public static async ValueTask<IKableSimpleClient> FromSessionAsync(
        IDeviceSession<string> session,
        ICommObserver? observer = null,
        CancellationToken ct = default)
    {
        var client = new KableSimpleClient(session, observer,
            (session as IRequestTimeoutProvider)?.DefaultRequestTimeout);
        await client.InitializeAsync(ct).ConfigureAwait(false);
        return client;
    }

}

