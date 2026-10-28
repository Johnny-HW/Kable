namespace Kable.Tests.Cases.Engine.Alarms;

using Kable.Engine;
using Kable.Engine.Alarms;
using Xunit;

public sealed class AlarmSpoolWorkerTests
{
    [Fact]
    public async Task Worker_owns_queue_persistence_and_final_accounting()
    {
        var saved = new List<string>();
        var worker = new AlarmSpoolWorker<string>(new KableSessionOptions<string>
        {
            AlarmOverflowMode = AlarmOverflowMode.SpoolToStorage,
            OnAlarmOverflowAsync = (message, _) => { saved.Add(message); return new ValueTask<bool>(true); }
        }, null, error => throw new InvalidOperationException("Unexpected failure", error));
        Assert.True(worker.TryEnqueue("one"));
        Assert.True(worker.TryEnqueue("two"));
        worker.Complete();
        worker.Start(default);
        await worker.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(new[] { "one", "two" }, saved);
        Assert.Equal(2, worker.Summary.StoredCount);
        Assert.Equal(0, worker.Summary.InDoubtCount);
        Assert.Equal(0, worker.Summary.UnprocessedCount);
    }
}
