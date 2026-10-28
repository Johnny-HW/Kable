namespace Kable.Engine;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;

public sealed partial class KableServer<TMessage>
{
    private async Task RunAcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            IConnectionContext context;
            try { context = await _listener.AcceptAsync(ct).ConfigureAwait(false); }
            catch (Exception) when (ct.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                Trace.TraceError("KableServer listener failed: {0}", error);
                // A failed listener cannot safely keep advertising a running server.
                _ = StopAsync();
                break;
            }

            try
            {
                if (ct.IsCancellationRequested || _registry.Count >= _options.MaxConnections)
                {
                    context.Abort("Server stopped or connection limit reached");
                    await context.DisposeAsync().ConfigureAwait(false);
                    continue;
                }
                var codec = _codecFactory() ?? throw new InvalidOperationException("Codec factory returned null.");
                var session = new KableServerSession<TMessage>(context, codec, _options.SessionQueueCapacity);
                if (!_registry.TryAdd(session))
                {
                    await session.DisposeAsync().ConfigureAwait(false);
                    continue;
                }
                SafeInvoke(ClientConnected, new ServerSessionEventArgs<TMessage>(session));
                _ = RunSessionPipelineAsync(session);
            }
            catch (Exception error)
            {
                Trace.TraceError("KableServer could not initialize connection: {0}", error);
                try
                {
                    context.Abort("Session initialization failed");
                    await context.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception cleanup) { Trace.TraceError("Connection cleanup failed: {0}", cleanup); }
            }
        }
    }

    private async Task RunSessionPipelineAsync(KableServerSession<TMessage> session)
    {
        await Task.Yield();
        var send = session.RunSendLoopAsync();
        var receive = RunSessionReceiveLoopAsync(session);
        string reason = "Session pipeline completed";
        try
        {
            var first = await Task.WhenAny(send, receive).ConfigureAwait(false);
            await first.ConfigureAwait(false);
        }
        catch (Exception error)
        {
            reason = error.Message;
            Trace.TraceError("KableServer session {0} failed: {1}", session.ConnectionId, error);
        }
        finally
        {
            try { session.Disconnect(reason); }
            catch (Exception error) { Trace.TraceError("Session abort failed: {0}", error); }
            try { await Task.WhenAll(send, receive).ConfigureAwait(false); }
            catch (Exception error) { Trace.TraceInformation("Session loops ended: {0}", error.Message); }
            try { await session.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) { Trace.TraceError("Session cleanup failed: {0}", error); }
            _registry.TryRemove(session.ConnectionId, out _);
            SafeInvoke(ClientDisconnected, new ServerSessionEventArgs<TMessage>(session, reason));
            session.MarkCompleted();
        }
    }

    private async Task RunSessionReceiveLoopAsync(KableServerSession<TMessage> session)
    {
        var reader = session.ConnectionContext.Input;
        var codec = session.Codec;
        try
        {
            while (!session.SessionClosed.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(session.SessionClosed).ConfigureAwait(false);
                var buffer = result.Buffer;
                while (!buffer.IsEmpty)
                {
                    long before = buffer.Length;
                    bool decoded = codec.TryDecode(ref buffer, out var message);
                    if (decoded)
                    {
                        if (buffer.Length >= before) throw new InvalidOperationException("Decoder made no progress.");
                        SafeInvoke(MessageReceived, new ServerMessageReceivedEventArgs<TMessage>(session, message));
                    }
                    else if (buffer.Length >= before) break;
                }
                reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted || result.IsCanceled) break;
            }
        }
        catch (OperationCanceledException) when (session.SessionClosed.IsCancellationRequested) { }
    }
}
