namespace Kable.Engine;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;

public sealed partial class KableServer<TMessage>
{
    public ValueTask SendAsync(string connectionId, TMessage message, CancellationToken ct = default)
    {
        EnsureRunning();
        if (!_registry.TryGet(connectionId, out var session) || session == null)
            throw new KeyNotFoundException($"Session {connectionId} was not found.");
        return session.SendAsync(message, ct);
    }

    public bool TryGetSession(string connectionId, out IServerSession<TMessage>? session)
    {
        var found = _registry.TryGet(connectionId, out var concrete);
        session = concrete;
        return found;
    }

    public async ValueTask<BroadcastResult> BroadcastAsync(TMessage message, CancellationToken ct = default)
    {
        EnsureRunning();
        ct.ThrowIfCancellationRequested();
        var sessions = _registry.Sessions;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(_options.DefaultBroadcastTimeout);
        var pending = new Task<bool>[sessions.Count];
        var ids = new string[sessions.Count];
        int index = 0;
        foreach (var session in sessions)
        {
            ids[index] = session.ConnectionId;
            pending[index++] = EnqueueAsync(session, message, deadline.Token);
        }
        var results = await Task.WhenAll(pending).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        int enqueued = 0;
        var failed = new List<string>();
        for (int i = 0; i < results.Length; i++)
        {
            if (results[i]) enqueued++;
            else failed.Add(ids[i]);
        }
        return new BroadcastResult(results.Length, enqueued, failed);
    }

    private static async Task<bool> EnqueueAsync(IServerSession<TMessage> session, TMessage message, CancellationToken ct)
    {
        try { await session.SendAsync(message, ct).ConfigureAwait(false); return true; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    private void EnsureRunning()
    {
        if (!IsRunning) throw new InvalidOperationException("Server is not running.");
    }
}
