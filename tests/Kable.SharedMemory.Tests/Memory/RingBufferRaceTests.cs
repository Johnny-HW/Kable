namespace Kable.SharedMemory.Tests.Memory;

using Xunit;

public class RingBufferRaceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicationRacingWaitRegistration_NeverLosesReadiness(bool space)
    {
        for (int iteration = 0; iteration < 300; iteration++)
        {
            using var pair = new RingBufferPair();
            pair.Prepare(space);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task producer = Task.Run(async () => { await start.Task; pair.Signal(space); });
            start.SetResult();
            Task<bool> wait = pair.WaitAsync(space).AsTask();
            Assert.True(await wait.WaitAsync(TimeSpan.FromSeconds(5)));
            await producer;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SignalCancellationAndDisposeRace_AlwaysFinishesSafely(bool space)
    {
        for (int iteration = 0; iteration < 200; iteration++)
        {
            using var pair = new RingBufferPair();
            pair.Prepare(space);
            using var cancellation = new CancellationTokenSource();
            Task<bool> wait = pair.WaitAsync(space, ct: cancellation.Token).AsTask();
            await Task.WhenAll(
                Task.Run(() => pair.Signal(space)),
                Task.Run(() => cancellation.Cancel()),
                Task.Run(() => pair.DisposeWaiter(space)));
            try { Assert.True(await wait.WaitAsync(TimeSpan.FromSeconds(5))); }
            catch (OperationCanceledException error) { Assert.Equal(cancellation.Token, error.CancellationToken); }
        }
    }

    [Fact]
    public async Task ConcurrentPointerQueriesAndDispose_KeepMemoryAliveUntilAccessEnds()
    {
        for (int iteration = 0; iteration < 100; iteration++)
        {
            using var pair = new RingBufferPair();
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task access = Task.Run(() =>
            {
                started.SetResult();
                for (int i = 0; i < 1000; i++)
                {
                    _ = pair.Writer.IsClosed;
                    pair.Writer.Write(new byte[] { 1 });
                    pair.Reader.Read(new byte[1]);
                }
            });
            await started.Task;
            pair.Writer.Dispose();
            pair.Reader.Dispose();
            await access.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(pair.Writer.IsClosed);
            Assert.Equal(0, pair.Writer.Write(new byte[1]));
            Assert.Equal(0, pair.Reader.Read(new byte[1]));
            pair.Writer.Close();
            pair.Reader.Close();
        }
    }
}
