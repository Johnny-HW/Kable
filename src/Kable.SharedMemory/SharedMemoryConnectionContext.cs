namespace Kable.SharedMemory;

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;
using Kable.SharedMemory.Memory;

/// <summary>
/// 두 개의 SharedMemoryRingBuffer(Inbound, Outbound)를 기반으로
/// System.IO.Pipelines(PipeReader, PipeWriter)를 투명하게 제공하는 IConnectionContext 구현체.
/// </summary>
public sealed class SharedMemoryConnectionContext : IConnectionContext
{
    private readonly SharedMemoryRingBuffer _inboundBuffer;   // 상대방이 쓰고 내가 읽음
    private readonly SharedMemoryRingBuffer _outboundBuffer;  // 내가 쓰고 상대방이 읽음
    private readonly Pipe _inboundPipe;
    private readonly Pipe _outboundPipe;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pumpInboundTask;
    private readonly Task _pumpOutboundTask;
    private int _isDisposed;

    public string ConnectionId { get; }
    public string EndpointDescription { get; }
    public PipeReader Input => _inboundPipe.Reader;
    public PipeWriter Output => _outboundPipe.Writer;
    public CancellationToken ConnectionClosed => _cts.Token;

    public SharedMemoryConnectionContext(
        SharedMemoryRingBuffer inboundBuffer,
        SharedMemoryRingBuffer outboundBuffer,
        string endpointDescription)
    {
        _inboundBuffer = inboundBuffer ?? throw new ArgumentNullException(nameof(inboundBuffer));
        _outboundBuffer = outboundBuffer ?? throw new ArgumentNullException(nameof(outboundBuffer));
        EndpointDescription = endpointDescription;
        ConnectionId = Guid.NewGuid().ToString("N");

        var pipeOptions = new PipeOptions(
            pauseWriterThreshold: 64 * 1024,
            resumeWriterThreshold: 32 * 1024,
            useSynchronizationContext: false);

        _inboundPipe = new Pipe(pipeOptions);
        _outboundPipe = new Pipe(pipeOptions);

        _pumpInboundTask = Task.Run(() => PumpInboundAsync(_inboundPipe.Writer, _cts.Token));
        _pumpOutboundTask = Task.Run(() => PumpOutboundAsync(_outboundPipe.Reader, _cts.Token));
    }

    private async Task PumpInboundAsync(PipeWriter writer, CancellationToken ct)
    {
        const int chunkSize = 4096;
        var tempBuffer = new byte[chunkSize];

        try
        {
            while (!ct.IsCancellationRequested && !_inboundBuffer.IsClosed)
            {
                int bytesRead = _inboundBuffer.Read(tempBuffer);
                if (bytesRead > 0)
                {
                    var mem = writer.GetMemory(bytesRead);
                    tempBuffer.AsSpan(0, bytesRead).CopyTo(mem.Span);
                    writer.Advance(bytesRead);

                    var flushResult = await writer.FlushAsync(ct).ConfigureAwait(false);
                    if (flushResult.IsCompleted || flushResult.IsCanceled)
                    {
                        break;
                    }
                }
                else
                {
                    // 데이터가 아직 없으면 대기
                    if (!_inboundBuffer.WaitForData(50, ct))
                    {
                        if (_inboundBuffer.IsClosed) break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            writer.Complete(ex);
            return;
        }

        writer.Complete();
    }

    private async Task PumpOutboundAsync(PipeReader reader, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && !_outboundBuffer.IsClosed)
            {
                var readResult = await reader.ReadAsync(ct).ConfigureAwait(false);
                var buffer = readResult.Buffer;

                if (buffer.IsEmpty && readResult.IsCompleted)
                {
                    break;
                }

                var seqReader = new SequenceReader<byte>(buffer);
                while (!seqReader.End)
                {
                    var unreadSpan = seqReader.UnreadSpan;
                    int written = _outboundBuffer.Write(unreadSpan);

                    if (written > 0)
                    {
                        seqReader.Advance(written);
                    }
                    else
                    {
                        // 버퍼가 꽉 차있으면 공간 대기
                        if (!_outboundBuffer.WaitForSpace(50, ct))
                        {
                            if (_outboundBuffer.IsClosed) break;
                        }
                    }
                }

                reader.AdvanceTo(seqReader.Position, buffer.End);

                if (readResult.IsCompleted)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            reader.Complete(ex);
            return;
        }

        reader.Complete();
    }

    public void Abort(string reason)
    {
        _cts.Cancel();
        _inboundBuffer.Close();
        _outboundBuffer.Close();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0) return;

        try
        {
            _cts.Cancel();
            _inboundPipe.Reader.CancelPendingRead();
            _outboundPipe.Writer.CancelPendingFlush();

            _inboundBuffer.Close();
            _outboundBuffer.Close();

            await Task.WhenAll(_pumpInboundTask, _pumpOutboundTask).ConfigureAwait(false);
        }
        catch
        {
            // Ignore cancellation on shutdown
        }
        finally
        {
            _inboundBuffer.Dispose();
            _outboundBuffer.Dispose();
            _cts.Dispose();
        }
    }
}
