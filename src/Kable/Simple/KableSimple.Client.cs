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

public static partial class KableSimple
{
    private sealed class KableSimpleClient : IKableSimpleClient
    {
        private readonly IDeviceSession<string> _session;
        private readonly ICommObserver? _observer;
        private readonly TimeSpan _defaultTimeout;
        private readonly CancellationTokenSource _cts = new();
        private Task? _readLoopTask;
        private bool _disposed;

        public event Action<string>? LineReceived;
        public event Action<Exception>? ErrorOccurred;
        public event Action<Exception?>? Disconnected;

        public bool IsConnected => _session.IsConnected;

        public KableSimpleClient(IDeviceSession<string> session, ICommObserver? observer = null, TimeSpan? defaultTimeout = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _observer = observer;
            _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(3);
            if (_defaultTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(defaultTimeout));
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
            var effectiveTimeout = timeout ?? _defaultTimeout;
            return await _session.RequestAsync<string>(command, effectiveTimeout, ct).ConfigureAwait(false);
        }

        private async Task ConsumeStreamAsync()
        {
            Exception? terminationReason = null;
            try
            {
                await foreach (var line in _session.Stream.WithCancellation(_cts.Token).ConfigureAwait(false))
                {
                    try
                    {
                        LineReceived?.Invoke(line);
                    }
                    catch (Exception handlerEx)
                    {
                        // 사용자 이벤트 핸들러 예외를 삼키지 않고 ErrorOccurred 및 옵저버로 보고
                        ReportError("USER_HANDLER_FAULT", handlerEx);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 정상 취소 / 종료
            }
            catch (Exception ex)
            {
                // 소켓 리셋, 프로토콜 위반, 타임아웃 등 통신 장애
                terminationReason = ex;
                ReportError("STREAM_FAULT", ex);
            }
            finally
            {
                try
                {
                    Disconnected?.Invoke(terminationReason);
                }
                catch (Exception disconnectEx)
                {
                    ReportError("DISCONNECT_HANDLER_FAULT", disconnectEx);
                }
            }
        }

        private void ReportError(string tag, Exception ex)
        {
            _observer?.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                tag, ReadOnlyMemory<byte>.Empty, $"{ex.GetType().Name}: {ex.Message}", TimeSpan.Zero, LogLevel.Error));

            try
            {
                ErrorOccurred?.Invoke(ex);
            }
            catch (Exception handlerEx)
            {
                // ErrorOccurred 핸들러 자체 오류는 무한 재귀를 막기 위해 ErrorOccurred를 재호출하지 않고 observer에 직접 기록
                _observer?.OnPacketTrace(new PacketTraceRecord(
                    DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                    "ERROR_HANDLER_FAULT", ReadOnlyMemory<byte>.Empty,
                    $"{handlerEx.GetType().Name} in ErrorOccurred handler: {handlerEx.Message}",
                    TimeSpan.Zero, LogLevel.Error));
            }
        }

        [Obsolete("동기 Dispose()는 Kable의 비차단 아키텍처에 위배될 수 있으므로 'await using' 또는 DisposeAsync()를 사용하십시오.")]
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
                try
                {
                    await _readLoopTask.ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    ReportError("DISPOSE_READ_LOOP_FAULT", ex);
                }
            }
            _cts.Dispose();
        }
    }
}

