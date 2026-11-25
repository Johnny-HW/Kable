namespace Kable.SharedMemory.Memory;

using System.Threading;
using System.Threading.Tasks;

internal static class RegisteredEventWait
{
    internal static async Task<bool> WaitAsync(WaitHandle handle, int timeoutMs, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        RegisteredWaitHandle registration = ThreadPool.RegisterWaitForSingleObject(handle,
            static (state, timedOut) => ((TaskCompletionSource<bool>)state!).TrySetResult(!timedOut),
            completion, timeoutMs, executeOnlyOnce: true);
        try
        {
#if NET6_0_OR_GREATER
            using var cancellation = ct.UnsafeRegister(
                static (state, token) => ((TaskCompletionSource<bool>)state!).TrySetCanceled(token), completion);
#else
            using var cancellation = ct.Register(
                static state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(), completion);
#endif
            return await completion.Task.ConfigureAwait(false);
        }
        finally { registration.Unregister(null); }
    }
}
