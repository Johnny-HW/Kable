using System.Diagnostics;
using Kable.Comparison.Adapters;
namespace Kable.Comparison.Faults;

public static class FaultProbes
{
    public static readonly string[] Scenarios = ["pre-cancel", "cancel-inflight", "deadline", "disconnect", "late-reply", "queued-disconnect"];
    public static IComparisonClient Create(string name, string mode) =>
        mode == "guarded" ? new GuardedClient(Clients.Create(name)) : Clients.Create(name);
    public static async Task<FaultRow> RunAsync(string name, string mode, string scenario, int trial)
    {
        string script = scenario switch
        {
            "pre-cancel" => "echo", "late-reply" => "late",
            "disconnect" or "queued-disconnect" => "close", _ => "stall"
        };
        await using var peer = new FaultPeer(script);
        var client = Create(name, mode);
        using var connect = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        FaultRow row;
        try
        {
            await client.ConnectAsync(peer.Port, connect.Token);
            row = await ProbeAsync(client, peer, name, mode, scenario, trial);
        }
        catch (Exception error)
        {
            row = new(name, mode, scenario, trial, "HarnessFailure", error.Message, 0, 0, peer.Commands.Count, error.GetType().FullName!);
        }
        var cleanup = Stopwatch.StartNew();
        try { await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); }
        catch (Exception error)
        {
            row = row with { Outcome = "CleanupFailure", Detail = row.Outcome + ": " + error.Message, ErrorType = error.GetType().FullName! };
        }
        return row with { CleanupMs = cleanup.Elapsed.TotalMilliseconds };
    }
    private static async Task<FaultRow> ProbeAsync(IComparisonClient client, FaultPeer peer,
        string name, string mode, string scenario, int trial)
    {
        using var cancel = new CancellationTokenSource();
        string outcome, detail = "", errorType = "";
        double latency, overshoot = 0;
        var watch = Stopwatch.StartNew();
        if (scenario == "pre-cancel")
        {
            cancel.Cancel();
            var result = await ObserveAsync(client.QueryAsync("A", cancel.Token));
            latency = watch.Elapsed.TotalMilliseconds;
            await Task.Delay(20); // Observe whether a command reached the peer despite pre-cancellation.
            outcome = result.Error is OperationCanceledException && peer.Commands.IsEmpty ? "Pass" : "UnexpectedSendOrResult";
            errorType = result.Error?.GetType().FullName ?? "";
        }
        else if (scenario == "deadline")
        {
            cancel.CancelAfter(100);
            var result = await ObserveAsync(client.QueryAsync("A", cancel.Token));
            latency = watch.Elapsed.TotalMilliseconds;
            overshoot = latency - 100;
            outcome = result.Error is OperationCanceledException ? "Pass" : result.State;
            errorType = result.Error?.GetType().FullName ?? "";
            detail = "100ms caller cancellation deadline; not native timeout semantics";
        }
        else
        {
            var query = client.QueryAsync("A", cancel.Token);
            await peer.Received.WaitAsync(TimeSpan.FromSeconds(3));
            if (scenario == "queued-disconnect")
            {
                // Only the guarded mode promises serialization. Raw is reported as unsupported.
                var queued = Enumerable.Range(0, 15).Select(i => client.QueryAsync("Q" + i, CancellationToken.None)).ToArray();
                watch.Restart();
                peer.Release();
                var results = await Task.WhenAll(queued.Prepend(query).Select(ObserveAsync));
                latency = watch.Elapsed.TotalMilliseconds;
                outcome = results.All(r => r.State == "Fault") ? "Pass" : "PendingOrUnexpectedSuccess";
                detail = $"completed={results.Count(r => r.State != "Watchdog")}/16; successes={results.Count(r => r.State == "Success")}";
                errorType = string.Join(";", results.Select(r => r.Error?.GetType().Name ?? r.State).Distinct());
            }
            else
            {
                watch.Restart();
                if (scenario == "disconnect") peer.Release();
                else cancel.Cancel();
                var result = await ObserveAsync(query);
                latency = watch.Elapsed.TotalMilliseconds;
                outcome = scenario == "disconnect"
                    ? result.State == "Fault" ? "Pass" : result.State
                    : result.Error is OperationCanceledException ? "Pass" : result.State;
                errorType = result.Error?.GetType().FullName ?? "";
                if (scenario == "late-reply" && outcome == "Pass")
                {
                    var next = client.QueryAsync("B", CancellationToken.None);
                    peer.Release(); // A's late reply cannot precede completion of A's cancellation.
                    var reply = await ObserveAsync(next);
                    outcome = reply.State switch
                    {
                        "Success" => reply.Value == "B" ? "CorrectReply" : "WrongReply",
                        "Fault" => "SafeRejected", _ => reply.State
                    };
                    detail = reply.Value ?? "B rejected; availability not scored as correctness failure";
                    errorType = reply.Error?.GetType().FullName ?? "";
                }
            }
        }
        return new(name, mode, scenario, trial, outcome, detail, latency, overshoot, peer.Commands.Count, errorType);
    }
    private static async Task<Observation> ObserveAsync(Task<string> task)
    {
        try { return new("Success", await task.WaitAsync(TimeSpan.FromSeconds(2)), null); }
        catch (TimeoutException error)
        {
            _ = task.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            return new("Watchdog", null, error);
        }
        catch (Exception error) { return new(error is OperationCanceledException ? "Canceled" : "Fault", null, error); }
    }
    private sealed record Observation(string State, string? Value, Exception? Error);
}
