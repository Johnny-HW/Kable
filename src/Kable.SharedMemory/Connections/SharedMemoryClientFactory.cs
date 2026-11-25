namespace Kable.SharedMemory.Connections;

using System.IO.Pipes;
using Kable.Core;

/// <summary>Connects a UI to a persistent listener using a fresh shared-memory generation.</summary>
public sealed class SharedMemoryClientFactory : IConnectionFactory
{
    private readonly string _channelName;
    private readonly int _connectTimeoutMs;

    public SharedMemoryClientFactory(string channelName, int connectTimeoutMs = 5000)
    {
        SharedMemoryCompat.ThrowIfNullOrWhiteSpace(channelName, nameof(channelName));
        SharedMemoryCompat.ThrowIfNegativeOrZero(connectTimeoutMs, nameof(connectTimeoutMs));
        _channelName = channelName;
        _connectTimeoutMs = connectTimeoutMs;
    }

    public async ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_connectTimeoutMs);
        var pipeOptions = PipeOptions.Asynchronous;
#if NETCOREAPP2_1_OR_GREATER || NET5_0_OR_GREATER
        pipeOptions |= PipeOptions.CurrentUserOnly;
#endif
        var pipe = new NamedPipeClientStream(".", SharedMemoryHandshake.ControlName(_channelName),
            PipeDirection.InOut, pipeOptions);
        SharedMemoryConnectionContext? context = null;
        bool published = false;
        try
        {
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            await SharedMemoryHandshake.WriteRequestAsync(pipe, timeout.Token).ConfigureAwait(false);
            Guid generation = await SharedMemoryHandshake.ReadReplyAsync(pipe, timeout.Token).ConfigureAwait(false);
            context = SharedMemoryTransport.ConnectClientEndpoint(SharedMemoryHandshake.SessionName(_channelName, generation));
            await SharedMemoryCompat.WriteAsync(pipe, new byte[] { 1 }, timeout.Token).ConfigureAwait(false);
            context.AttachControlPipe(pipe, generation);
            published = true;
            return context;
        }
        catch (OperationCanceledException error) when (!ct.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new TimeoutException("The shared memory connection timed out.", error);
        }
        finally
        {
            if (!published)
            {
                pipe.Dispose();
                if (context != null) await context.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
