namespace Kable.SharedMemory.Connections;

using System.IO.Pipes;
using Kable.Core;

/// <summary>Accepts one UI at a time without owning or restarting the physical device session.</summary>
public sealed partial class SharedMemoryServerListener : IConnectionListener
{
    private readonly string _channelName;
    private readonly int _capacity;
    private readonly int _handshakeTimeoutMs;
    private readonly EventWaitHandle _owner;
    private readonly SemaphoreSlim _acceptGate = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly CancellationToken _stopped;
    private readonly object _sync = new();
    private SharedMemoryConnectionContext? _active;
    private NamedPipeServerStream? _pending;
    private Task? _disposeTask;
    private bool _stopRequested;

    public SharedMemoryServerListener(string channelName, int bufferCapacityPowerOfTwo = 64 * 1024,
        int handshakeTimeoutMs = 5000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(handshakeTimeoutMs);
        if (bufferCapacityPowerOfTwo <= 0 || (bufferCapacityPowerOfTwo & (bufferCapacityPowerOfTwo - 1)) != 0)
            throw new ArgumentException("Capacity must be a positive power of two.", nameof(bufferCapacityPowerOfTwo));
        _channelName = channelName;
        _capacity = bufferCapacityPowerOfTwo;
        _handshakeTimeoutMs = handshakeTimeoutMs;
        _stopped = _stop.Token;
        _owner = new EventWaitHandle(false, EventResetMode.ManualReset,
            SharedMemoryHandshake.ControlName(channelName) + ".owner", out bool created);
        if (!created)
        {
            _owner.Dispose();
            _stop.Dispose();
            throw new IOException("A shared memory listener already owns this logical channel.");
        }
    }

    public async ValueTask<IConnectionContext> AcceptAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stopped);
        await _acceptGate.WaitAsync(linked.Token).ConfigureAwait(false);
        NamedPipeServerStream? pipe = null;
        SharedMemoryConnectionContext? context = null;
        bool published = false;
        try
        {
            if (_active != null)
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, _active.ConnectionClosed).WaitAsync(linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_active.ConnectionClosed.IsCancellationRequested && !linked.IsCancellationRequested)
                {
                    // The previous UI closed; its generation can now be released.
                }
                await _active.DisposeAsync().ConfigureAwait(false);
                _active = null;
            }
            linked.Token.ThrowIfCancellationRequested();
            pipe = new NamedPipeServerStream(SharedMemoryHandshake.ControlName(_channelName), PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            lock (_sync)
            {
                linked.Token.ThrowIfCancellationRequested();
                _pending = pipe;
            }
            await pipe.WaitForConnectionAsync(linked.Token).ConfigureAwait(false);
            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
            handshake.CancelAfter(_handshakeTimeoutMs);
            try
            {
                await SharedMemoryHandshake.ReadRequestAsync(pipe, handshake.Token).ConfigureAwait(false);
                Guid generation = Guid.NewGuid();
                context = SharedMemoryTransport.CreateServerEndpoint(
                    SharedMemoryHandshake.SessionName(_channelName, generation), _capacity);
                await SharedMemoryHandshake.WriteReplyAsync(pipe, generation, handshake.Token).ConfigureAwait(false);
                await SharedMemoryHandshake.ReadAckAsync(pipe, handshake.Token).ConfigureAwait(false);
                context.AttachControlPipe(pipe, generation);
            }
            catch (OperationCanceledException error) when (!linked.IsCancellationRequested && handshake.IsCancellationRequested)
            {
                throw new TimeoutException("The shared memory handshake timed out.", error);
            }
            lock (_sync)
            {
                linked.Token.ThrowIfCancellationRequested();
                _pending = null;
                _active = context;
                published = true;
            }
            return context;
        }
        catch (Exception) when (linked.IsCancellationRequested)
        {
            throw new OperationCanceledException(linked.Token);
        }
        finally
        {
            lock (_sync) { if (ReferenceEquals(_pending, pipe)) _pending = null; }
            try
            {
                if (!published)
                {
                    pipe?.Dispose();
                    if (context != null) await context.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally { _acceptGate.Release(); }
        }
    }
}
