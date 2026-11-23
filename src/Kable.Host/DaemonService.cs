namespace Kable.Host;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;
using Kable.Transport.Ipc;

public sealed class DaemonService
{
    private readonly string _pipeName;
    private readonly ConcurrentDictionary<string, (IConnectionContext Context, Task Task)> _activeSessions = new();

    public int ActiveSessionCount => _activeSessions.Count;

    public DaemonService(string pipeName)
    {
        _pipeName = pipeName;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        await using var listener = new IpcNamedPipeServerListener(_pipeName);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var context = await listener.AcceptAsync(ct).ConfigureAwait(false);
                var registered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                var sessionTask = Task.Run(async () =>
                {
                    await registered.Task.ConfigureAwait(false);
                    try
                    {
                        var reader = context.Input;
                        var writer = context.Output;

                        while (!ct.IsCancellationRequested && !context.ConnectionClosed.IsCancellationRequested)
                        {
                            var readResult = await reader.ReadAsync(ct).ConfigureAwait(false);
                            var buffer = readResult.Buffer;

                            if (buffer.Length > 0)
                            {
                                foreach (var segment in buffer)
                                {
                                    await writer.WriteAsync(segment, ct).ConfigureAwait(false);
                                }
                                await writer.FlushAsync(ct).ConfigureAwait(false);
                            }

                            reader.AdvanceTo(buffer.End);
                            if (readResult.IsCompleted || readResult.IsCanceled) break;
                        }
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DaemonService] Session {context.ConnectionId} disconnected: {ex.Message}");
                    }
                    finally
                    {
                        try
                        {
                            await context.DisposeAsync().ConfigureAwait(false);
                        }
                        finally
                        {
                            _activeSessions.TryRemove(context.ConnectionId, out _);
                        }
                    }
                }, CancellationToken.None);

                _activeSessions[context.ConnectionId] = (context, sessionTask);
                registered.TrySetResult(true);
            }
        }
        catch (OperationCanceledException)
        {
            if (!_activeSessions.IsEmpty)
            {
                var snapshot = _activeSessions.Values.ToArray();
                var waitAll = Task.WhenAll(snapshot.Select(static s => s.Task));
                var completed = await Task.WhenAny(waitAll, Task.Delay(2000, CancellationToken.None)).ConfigureAwait(false);
                if (completed != waitAll)
                {
                    foreach (var entry in snapshot)
                    {
                        try { entry.Context.Abort("Daemon service stopped."); }
                        catch (Exception ex) when (ex is not OutOfMemoryException)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DaemonService] Abort on shutdown failed: {ex.Message}");
                        }
                    }
                    await Task.WhenAny(waitAll, Task.Delay(1000, CancellationToken.None)).ConfigureAwait(false);
                }
            }
        }
    }
}
