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
public sealed partial class SharedMemoryConnectionContext : IConnectionContext
{
    private readonly SharedMemoryRingBuffer _inboundBuffer;   // 상대방이 쓰고 내가 읽음
    private readonly SharedMemoryRingBuffer _outboundBuffer;  // 내가 쓰고 상대방이 읽음
    private readonly Pipe _inboundPipe;
    private readonly Pipe _outboundPipe;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pumpInboundTask;
    private readonly Task _pumpOutboundTask;

    public string ConnectionId { get; }
    public string EndpointDescription { get; }
    public PipeReader Input => _inboundPipe.Reader;
    public PipeWriter Output => _outboundPipe.Writer;
    public CancellationToken ConnectionClosed { get; }

    public SharedMemoryConnectionContext(
        SharedMemoryRingBuffer inboundBuffer,
        SharedMemoryRingBuffer outboundBuffer,
        string endpointDescription)
    {
        _inboundBuffer = inboundBuffer ?? throw new ArgumentNullException(nameof(inboundBuffer));
        _outboundBuffer = outboundBuffer ?? throw new ArgumentNullException(nameof(outboundBuffer));
        EndpointDescription = endpointDescription;
        ConnectionId = Guid.NewGuid().ToString("N");
        ConnectionClosed = _cts.Token;

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

        try
        {
            while (!ct.IsCancellationRequested && !_inboundBuffer.IsClosed)
            {
                var memory = writer.GetMemory(chunkSize).Slice(0, chunkSize);
                int bytesRead = _inboundBuffer.Read(memory.Span);
                if (bytesRead > 0)
                {
                    writer.Advance(bytesRead);

                    var flushResult = await writer.FlushAsync(ct).ConfigureAwait(false);
                    if (flushResult.IsCompleted || flushResult.IsCanceled)
                    {
                        break;
                    }
                }
                else await _inboundBuffer.WaitForDataAsync(ct: ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            StopPumps();
            writer.Complete(ex);
            return;
        }

        StopPumps();
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

                if (readResult.IsCanceled || (buffer.IsEmpty && readResult.IsCompleted))
                {
                    break;
                }

                long consumed = 0;
                foreach (var segment in buffer)
                {
                    var remaining = segment;
                    while (!remaining.IsEmpty && !ct.IsCancellationRequested && !_outboundBuffer.IsClosed)
                    {
                        int written = _outboundBuffer.Write(remaining.Span);
                        if (written > 0)
                        {
                            remaining = remaining.Slice(written);
                            consumed += written;
                        }
                        else await _outboundBuffer.WaitForSpaceAsync(ct: ct).ConfigureAwait(false);
                    }
                    if (!remaining.IsEmpty) break;
                }

                // AdvanceTo permits buffer reuse; inspect its length before releasing it.
                bool finished = readResult.IsCompleted || consumed < buffer.Length;
                reader.AdvanceTo(buffer.GetPosition(consumed), buffer.End);

                if (finished)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            StopPumps();
            reader.Complete(ex);
            return;
        }

        StopPumps();
        reader.Complete();
    }
}
