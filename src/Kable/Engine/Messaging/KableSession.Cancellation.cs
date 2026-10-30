namespace Kable.Engine;

using System.Threading;
using System.Threading.Tasks;

public sealed partial class KableSession<TMessage>
{
    private static async Task<T> WaitWithCancellationAsync<T>(Task<T> task, CancellationToken ct)
    {
#if NET6_0_OR_GREATER
        return await task.WaitAsync(ct).ConfigureAwait(false);
#else
        if (task.IsCompleted)
        {
            return await task.ConfigureAwait(false);
        }

        if (!ct.CanBeCanceled) return await task.ConfigureAwait(false);
        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = ct.Register(state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancelled);
        var delayTask = cancelled.Task;
        var completed = await Task.WhenAny(task, delayTask).ConfigureAwait(false);
        if (completed == delayTask)
        {
            ct.ThrowIfCancellationRequested();
        }
        return await task.ConfigureAwait(false);
#endif
    }

    private static async Task WaitWithCancellationAsync(Task task, CancellationToken ct)
    {
#if NET6_0_OR_GREATER
        await task.WaitAsync(ct).ConfigureAwait(false);
#else
        if (task.IsCompleted)
        {
            await task.ConfigureAwait(false);
            return;
        }

        if (!ct.CanBeCanceled) { await task.ConfigureAwait(false); return; }
        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = ct.Register(state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancelled);
        var delayTask = cancelled.Task;
        var completed = await Task.WhenAny(task, delayTask).ConfigureAwait(false);
        if (completed == delayTask)
        {
            ct.ThrowIfCancellationRequested();
        }
        await task.ConfigureAwait(false);
#endif
    }

}
