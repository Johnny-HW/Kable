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
/// Pipelines와 반응형 스트림 학습 곡선 없이 즉시 장비와 통신할 수 있는 심플 파사드
/// 동기 블로킹(.Result / .Wait())을 엄격히 배제하고 async/await 기반의 안전한 비차단 API를 제공합니다.
/// </summary>
public static class KableSimple
{
    /// <summary>
    /// 시리얼(RS-232/422/485) 포트를 개방하고 KableSimple 클라이언트를 시작합니다.
    /// </summary>
    public static async ValueTask<IKableSimpleClient> OpenSerialAsync(
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
        var codec = new AsciiLineCodec(delimiter, encoding);
        var builder = new KableClientBuilder<string>()
            .UseSerialPort(portName, baudRate, parity, dataBits, stopBits)
            .UseCodec(codec);

        if (observer != null)
        {
            builder.UseObserver(observer);
        }

        var session = builder.Build();
        var client = new KableSimpleClient(session);
        await client.InitializeAsync(ct).ConfigureAwait(false);
        return client;
    }

    /// <summary>
    /// TCP/IP 소켓을 연결하고 KableSimple 클라이언트를 시작합니다.
    /// </summary>
    public static async ValueTask<IKableSimpleClient> OpenTcpAsync(
        string host,
        int port,
        byte delimiter = 0x0A,
        Encoding? encoding = null,
        ICommObserver? observer = null,
        CancellationToken ct = default)
    {
        var codec = new AsciiLineCodec(delimiter, encoding);
        var builder = new KableClientBuilder<string>()
            .UseTcp(host, port)
            .UseCodec(codec);

        if (observer != null)
        {
            builder.UseObserver(observer);
        }

        var session = builder.Build();
        var client = new KableSimpleClient(session);
        await client.InitializeAsync(ct).ConfigureAwait(false);
        return client;
    }

    /// <summary>
    /// 기존 구성된 IDeviceSession&lt;string&gt;을 심플 클라이언트 래퍼로 감싸 시작합니다.
    /// </summary>
    public static async ValueTask<IKableSimpleClient> FromSessionAsync(
        IDeviceSession<string> session,
        CancellationToken ct = default)
    {
        var client = new KableSimpleClient(session);
        await client.InitializeAsync(ct).ConfigureAwait(false);
        return client;
    }

    private sealed class KableSimpleClient : IKableSimpleClient
    {
        private readonly IDeviceSession<string> _session;
        private readonly CancellationTokenSource _cts = new();
        private Task? _readLoopTask;
        private bool _disposed;

        public event Action<string>? LineReceived;
        public event Action? Disconnected;

        public bool IsConnected => _session.IsConnected;

        public KableSimpleClient(IDeviceSession<string> session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        internal async ValueTask InitializeAsync(CancellationToken ct)
        {
            await _session.StartAsync(ct).ConfigureAwait(false);
            _readLoopTask = Task.Run(ConsumeStreamAsync);
        }

        public ValueTask SendLineAsync(string command, CancellationToken ct = default)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
            return _session.SendAsync(command, ct);
        }

        public async ValueTask<string> QueryAsync(string command, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
            var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(3);
            return await _session.RequestAsync<string>(command, effectiveTimeout, ct).ConfigureAwait(false);
        }

        private async Task ConsumeStreamAsync()
        {
            try
            {
                await foreach (var line in _session.Stream.WithCancellation(_cts.Token).ConfigureAwait(false))
                {
                    try
                    {
                        LineReceived?.Invoke(line);
                    }
                    catch
                    {
                        // 사용자 이벤트 핸들러 예외가 백그라운드 소비 루프를 중단시키지 않도록 격리
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 정상 종료
            }
            catch
            {
                // 통신 단절 등 예외
            }
            finally
            {
                try
                {
                    Disconnected?.Invoke();
                }
                catch
                {
                    // 격리
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _session.Dispose();
            _cts.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            await _session.DisposeAsync().ConfigureAwait(false);
            if (_readLoopTask != null)
            {
                try { await _readLoopTask.ConfigureAwait(false); } catch { }
            }
            _cts.Dispose();
        }
    }
}
