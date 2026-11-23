namespace Kable.Grpc;

using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using global::Grpc.Core;
using global::Kable.Core;
using Kable.Grpc.Protos;

/// <summary>
/// gRPC 양방향 스트리밍을 System.IO.Pipelines(PipeReader, PipeWriter)로 투명하게 브리징하는 ConnectionContext 구현체
/// </summary>
public sealed class GrpcConnectionContext : IConnectionContext
{
    private readonly Pipe _inboundPipe;
    private readonly Pipe _outboundPipe;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pumpInboundTask;
    private readonly Task _pumpOutboundTask;
    private int _isDisposed;
    private readonly object _lifetime = new();
    private bool _aborted;
    private IDisposable? _callScope;
    private Task? _disposeTask;

    public string ConnectionId { get; }
    public string EndpointDescription { get; }
    public PipeReader Input => _inboundPipe.Reader;
    public PipeWriter Output => _outboundPipe.Writer;
    public CancellationToken ConnectionClosed => _cts.Token;

    public GrpcConnectionContext(
        IAsyncStreamReader<KablePacket> responseStream,
        IClientStreamWriter<KablePacket> requestStream,
        string endpointDescription,
        IDisposable? callScope = null)
    {
        ConnectionId = Guid.NewGuid().ToString("N");
        EndpointDescription = endpointDescription;
        _callScope = callScope;

        var pipeOptions = new PipeOptions(
            pauseWriterThreshold: 64 * 1024,
            resumeWriterThreshold: 32 * 1024,
            useSynchronizationContext: false);

        _inboundPipe = new Pipe(pipeOptions);
        _outboundPipe = new Pipe(pipeOptions);

        _pumpInboundTask = PumpInboundAsync(responseStream, _inboundPipe.Writer, _cts.Token);
        _pumpOutboundTask = PumpOutboundAsync(_outboundPipe.Reader, requestStream, _cts.Token);
    }

    public GrpcConnectionContext(
        IAsyncStreamReader<KablePacket> requestStream,
        IServerStreamWriter<KablePacket> responseStream,
        string endpointDescription)
    {
        ConnectionId = Guid.NewGuid().ToString("N");
        EndpointDescription = endpointDescription;

        var pipeOptions = new PipeOptions(
            pauseWriterThreshold: 64 * 1024,
            resumeWriterThreshold: 32 * 1024,
            useSynchronizationContext: false);

        _inboundPipe = new Pipe(pipeOptions);
        _outboundPipe = new Pipe(pipeOptions);

        _pumpInboundTask = PumpInboundAsync(requestStream, _inboundPipe.Writer, _cts.Token);
        _pumpOutboundTask = PumpOutboundServerAsync(_outboundPipe.Reader, responseStream, _cts.Token);
    }

    private static async Task PumpInboundAsync(
        IAsyncStreamReader<KablePacket> stream,
        PipeWriter writer,
        CancellationToken ct)
    {
        try
        {
            while (await stream.MoveNext(ct).ConfigureAwait(false))
            {
                var packet = stream.Current;
                if (packet != null && packet.Payload.Length > 0)
                {
                    var memory = writer.GetMemory(packet.Payload.Length);
                    packet.Payload.Span.CopyTo(memory.Span);
                    writer.Advance(packet.Payload.Length);

                    var flushResult = await writer.FlushAsync(ct).ConfigureAwait(false);
                    if (flushResult.IsCompleted || flushResult.IsCanceled)
                        break;
                }
            }
            await writer.CompleteAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await writer.CompleteAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await writer.CompleteAsync(ex).ConfigureAwait(false);
        }
    }

    private async Task PumpOutboundAsync(
        PipeReader reader,
        IClientStreamWriter<KablePacket> stream,
        CancellationToken ct)
    {
        long sequence = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var readResult = await reader.ReadAsync(ct).ConfigureAwait(false);
                var buffer = readResult.Buffer;

                if (buffer.Length > 0)
                {
                    var packet = new KablePacket
                    {
                        Sequence = Interlocked.Increment(ref sequence),
                        Payload = ByteString.CopyFrom(System.Buffers.BuffersExtensions.ToArray(buffer))
                    };

                    await stream.WriteAsync(packet).ConfigureAwait(false);
                    reader.AdvanceTo(buffer.End);
                }

                if (readResult.IsCompleted || readResult.IsCanceled)
                    break;
            }

            try
            {
                await stream.CompleteAsync().ConfigureAwait(false);
            }
            catch (Exception completeEx) when (completeEx is RpcException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
            {
                System.Diagnostics.Debug.WriteLine($"[GrpcConnectionContext] Outbound stream already closed: {completeEx.Message}");
            }

            await reader.CompleteAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await reader.CompleteAsync(ex).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _callScope, null)?.Dispose();
        }
    }

    private static async Task PumpOutboundServerAsync(
        PipeReader reader,
        IServerStreamWriter<KablePacket> stream,
        CancellationToken ct)
    {
        long sequence = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var readResult = await reader.ReadAsync(ct).ConfigureAwait(false);
                var buffer = readResult.Buffer;

                if (buffer.Length > 0)
                {
                    var packet = new KablePacket
                    {
                        Sequence = Interlocked.Increment(ref sequence),
                        Payload = ByteString.CopyFrom(System.Buffers.BuffersExtensions.ToArray(buffer))
                    };

#if NETSTANDARD2_0
                    await stream.WriteAsync(packet).ConfigureAwait(false);
#else
                    await stream.WriteAsync(packet, ct).ConfigureAwait(false);
#endif
                    reader.AdvanceTo(buffer.End);
                }

                if (readResult.IsCompleted || readResult.IsCanceled)
                    break;
            }

            await reader.CompleteAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await reader.CompleteAsync(ex).ConfigureAwait(false);
        }
    }

    public void Abort(string reason)
    {
        lock (_lifetime)
        {
            if (_isDisposed != 0 || _aborted) return;
            _aborted = true;
            try { _cts.Cancel(); }
            finally
            {
                try { Interlocked.Exchange(ref _callScope, null)?.Dispose(); }
                finally
                {
                    _inboundPipe.Writer.CancelPendingFlush();
                    _outboundPipe.Reader.CancelPendingRead();
                }
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifetime) return new ValueTask(_disposeTask ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        Exception? cancellationError = null;
        lock (_lifetime)
        {
            if (_isDisposed != 0) return;
            _isDisposed = 1;
            try { _cts.Cancel(); }
            catch (Exception error) { cancellationError = error; }
            try { Interlocked.Exchange(ref _callScope, null)?.Dispose(); }
            catch (Exception error) { cancellationError = cancellationError == null ? error : new AggregateException(cancellationError, error); }
        }
        try
        {

            try { await _inboundPipe.Reader.CompleteAsync().ConfigureAwait(false); }
            catch (Exception pipeEx) when (pipeEx is not OutOfMemoryException) { System.Diagnostics.Debug.WriteLine($"[GrpcConnectionContext] Inbound pipe complete error: {pipeEx.Message}"); }
            try { await _outboundPipe.Writer.CompleteAsync().ConfigureAwait(false); }
            catch (Exception pipeEx) when (pipeEx is not OutOfMemoryException) { System.Diagnostics.Debug.WriteLine($"[GrpcConnectionContext] Outbound pipe complete error: {pipeEx.Message}"); }

            try
            {
                await Task.WhenAll(_pumpInboundTask, _pumpOutboundTask).ConfigureAwait(false);
            }
            catch (Exception pumpEx) when (pumpEx is not OutOfMemoryException)
            {
                System.Diagnostics.Debug.WriteLine($"[GrpcConnectionContext] Pump shutdown observed: {pumpEx.Message}");
            }

        }
        finally { _cts.Dispose(); }
        if (cancellationError != null) throw cancellationError;
    }
}
