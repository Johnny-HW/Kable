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
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(connectTimeoutMs);
        _channelName = channelName;
        _connectTimeoutMs = connectTimeoutMs;
    }

    public async ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_connectTimeoutMs);
        var pipe = new NamedPipeClientStream(".", SharedMemoryHandshake.ControlName(_channelName),
            PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        SharedMemoryConnectionContext? context = null;
        bool published = false;
        try
        {
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            await SharedMemoryHandshake.WriteRequestAsync(pipe, timeout.Token).ConfigureAwait(false);
            Guid generation = await SharedMemoryHandshake.ReadReplyAsync(pipe, timeout.Token).ConfigureAwait(false);
            context = SharedMemoryTransport.ConnectClientEndpoint(SharedMemoryHandshake.SessionName(_channelName, generation));
            await pipe.WriteAsync(new byte[] { 1 }, timeout.Token).ConfigureAwait(false);
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
