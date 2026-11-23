namespace Kable.Transport.Ipc;

using System;
using System.IO.Pipes;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;

public sealed class IpcNamedPipeConnectionContext : IConnectionContext
{
    private readonly Stream _pipeStream;
    private readonly CancellationTokenSource _cts = new();
    private int _isDisposed;
    private readonly object _lifetime = new();
    private bool _aborted;
    private Task? _disposeTask;

    public string ConnectionId { get; }
    public string EndpointDescription { get; }
    public PipeReader Input { get; }
    public PipeWriter Output { get; }
    public CancellationToken ConnectionClosed => _cts.Token;

    public IpcNamedPipeConnectionContext(Stream pipeStream, string pipeName)
    {
        _pipeStream = pipeStream;
        ConnectionId = Guid.NewGuid().ToString("N");
        EndpointDescription = $"ipc://local/{pipeName}";

        Input = PipeReader.Create(pipeStream);
        Output = PipeWriter.Create(pipeStream);
    }

    public void Abort(string reason)
    {
        lock (_lifetime)
        {
            if (_isDisposed != 0 || _aborted) return;
            _aborted = true;
            try { _cts.Cancel(); }
            finally { _pipeStream.Close(); }
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
        }
        try
        {
            try { await Input.CompleteAsync().ConfigureAwait(false); } catch (Exception) { /* PipeReader already completed */ }
            try { await Output.CompleteAsync().ConfigureAwait(false); } catch (Exception) { /* PipeWriter already completed */ }
#if NETSTANDARD2_0
            _pipeStream.Dispose();
#else
            await _pipeStream.DisposeAsync().ConfigureAwait(false);
#endif
        }
        finally { _cts.Dispose(); }
        if (cancellationError != null) throw cancellationError;
    }
}

public sealed class IpcNamedPipeClientFactory : IConnectionFactory
{
    private readonly string _pipeName;
    private readonly string _serverName;
    private readonly int _timeoutMs;

    public IpcNamedPipeClientFactory(string pipeName, string serverName = ".", int timeoutMs = 5000)
    {
        _pipeName = pipeName;
        _serverName = serverName;
        _timeoutMs = timeoutMs;
    }

    public async ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        var pipeStream = new NamedPipeClientStream(
            _serverName,
            _pipeName,
            PipeDirection.InOut,
            System.IO.Pipes.PipeOptions.Asynchronous);

        try
        {
            await pipeStream.ConnectAsync(_timeoutMs, ct).ConfigureAwait(false);
            return new IpcNamedPipeConnectionContext(pipeStream, _pipeName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IpcNamedPipeConnectionFactory] ConnectAsync failed: {ex.Message}");
            pipeStream.Dispose();
            throw;
        }
    }
}

public sealed class IpcNamedPipeServerListener : IConnectionListener
{
    private readonly string _pipeName;
    private NamedPipeServerStream? _currentServerStream;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _acceptSync = new();
    private bool _stopped;
    private bool _disposeRequested;
    private bool _sourceDisposed;

    public IpcNamedPipeServerListener(string pipeName)
    {
        _pipeName = pipeName;
    }

    public async ValueTask<IConnectionContext> AcceptAsync(CancellationToken ct = default)
    {
        var serverStream = new NamedPipeServerStream(
            _pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            System.IO.Pipes.PipeOptions.Asynchronous);

        try
        {
            lock (_acceptSync)
            {
                if (_stopped) throw new OperationCanceledException("Listener stopped.");
                if (_currentServerStream != null) throw new InvalidOperationException("An accept is already pending.");
                _currentServerStream = serverStream;
            }
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
            using var registration = linkedCts.Token.Register(() => serverStream.Dispose());
            try
            {
                await serverStream.WaitForConnectionAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (Exception) when (linkedCts.IsCancellationRequested)
            {
                throw new OperationCanceledException(linkedCts.Token);
            }
            lock (_acceptSync)
            {
                linkedCts.Token.ThrowIfCancellationRequested();
                if (_stopped) throw new OperationCanceledException("Listener stopped.");
                _currentServerStream = null; // Ownership passes to the returned context.
            }
            return new IpcNamedPipeConnectionContext(serverStream, _pipeName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IpcNamedPipeServerListener] AcceptAsync failed: {ex.Message}");
            serverStream.Dispose();
            throw;
        }
        finally
        {
            lock (_acceptSync)
            {
                if (ReferenceEquals(_currentServerStream, serverStream)) _currentServerStream = null;
                DisposeStoppedSource();
            }
        }
    }

    public void Stop()
    {
        lock (_acceptSync)
        {
            if (_stopped) return;
            _stopped = true;
            _cts.Cancel();
            _currentServerStream?.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        lock (_acceptSync)
        {
            _disposeRequested = true;
            DisposeStoppedSource();
        }
        return default;
    }

    private void DisposeStoppedSource()
    {
        if (_disposeRequested && _currentServerStream == null && !_sourceDisposed)
        {
            _sourceDisposed = true;
            _cts.Dispose();
        }
    }
}
