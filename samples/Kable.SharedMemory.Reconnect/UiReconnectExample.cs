namespace Kable.Samples.SharedMemoryReconnect;

using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.SharedMemory.Connections;

public static class UiReconnectExample
{
    /// <summary>The caller owns an already-started device session independently of UI connections.</summary>
    public static async Task RunHostAsync(IDeviceSession<string> device, string channelName,
        Action<Exception> reportIpcError, CancellationToken stop)
    {
        await using var listener = new SharedMemoryServerListener(channelName);
        while (!stop.IsCancellationRequested)
        {
            IConnectionContext ui;
            try { ui = await listener.AcceptAsync(stop); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (IOException error) { reportIpcError(error); continue; }
            catch (TimeoutException error) { reportIpcError(error); continue; }
            await using var uiScope = ui;
            using var uiStop = CancellationTokenSource.CreateLinkedTokenSource(stop, ui.ConnectionClosed);
            var codec = new AsciiLineCodec();
            try
            {
                while (!uiStop.IsCancellationRequested)
                {
                    string? command = await ReadCommandAsync(ui, codec, uiStop.Token);
                    if (command == null || ui.ConnectionClosed.IsCancellationRequested) break;
                    // Once dispatched, UI loss must not cancel this FIFO device request or replay it.
                    string reply = await device.RequestAsync<string>(command, TimeSpan.FromSeconds(5), stop);
                    if (!await ReplyToOriginalUiAsync(ui, codec, reply, uiStop.Token)) break;
                }
            }
            catch (OperationCanceledException) when (uiStop.IsCancellationRequested) { }
            catch (IOException error) when (ui.ConnectionClosed.IsCancellationRequested) { reportIpcError(error); }
        }
    }

    /// <summary>Example application protocol: STATUS retrieves the current state after each reconnect.</summary>
    public static async Task<(KableSession<string> Session, string InitialState)> OpenUiAsync(
        string channelName, CancellationToken ct)
    {
        var session = new KableSession<string>(new SharedMemoryClientFactory(channelName), new AsciiLineCodec());
        try
        {
            await session.StartAsync(ct);
            string state = await session.RequestAsync<string>("STATUS", TimeSpan.FromSeconds(5), ct);
            return (session, state);
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    private static async Task<string?> ReadCommandAsync(IConnectionContext ui, AsciiLineCodec codec, CancellationToken ct)
    {
        while (true)
        {
            var read = await ui.Input.ReadAsync(ct);
            var remaining = read.Buffer;
            if (codec.TryDecode(ref remaining, out string command))
            {
                ui.Input.AdvanceTo(remaining.Start, remaining.Start);
                return command;
            }
            ui.Input.AdvanceTo(remaining.Start, remaining.End);
            if (read.IsCompleted) return null;
        }
    }

    private static async Task<bool> ReplyToOriginalUiAsync(IConnectionContext ui, AsciiLineCodec codec, string reply, CancellationToken ct)
    {
        if (ui.ConnectionClosed.IsCancellationRequested) return false;
        try
        {
            codec.Encode(reply, ui.Output);
            var result = await ui.Output.FlushAsync(ct);
            return !result.IsCanceled && !result.IsCompleted;
        }
        catch (InvalidOperationException) when (ui.ConnectionClosed.IsCancellationRequested) { return false; }
    }
}
