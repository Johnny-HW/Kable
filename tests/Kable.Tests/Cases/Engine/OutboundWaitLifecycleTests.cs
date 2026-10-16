namespace Kable.Tests.Cases.Engine;

using System.Reflection;
using System.Threading.Channels;
using Kable.Codecs;
using Kable.Engine;
using Kable.Tests.Fixtures;
using Xunit;

public sealed class OutboundWaitLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedSends_ReuseIdleQueueWait_AndObserveItOnStop(bool urgent)
    {
        var factory = new TestMemoryConnectionFactory();
        await using var session = new KableSession<string>(factory, new AsciiLineCodec(delimiter: 0x0A));
        var idle = TrackChannel(session, urgent ? "_outboundNormalQueue" : "_outboundUrgentQueue");
        await session.StartAsync();

        for (int i = 0; i < 32; i++)
        {
            if (urgent) await session.SendUrgentAsync("STOP").AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            else await session.SendAsync("SEND").AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            var read = await factory.Context.RemoteRead.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(read.Buffer.IsEmpty);
            factory.Context.RemoteRead.AdvanceTo(read.Buffer.End);
        }

        await session.StopAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, idle.Registrations);
        Assert.Equal(0, idle.Pending);
    }

    private static ITrackedChannel TrackChannel(KableSession<string> session, string fieldName)
    {
        // Wrap the private channel to observe registrations without a public diagnostics API.
        var field = typeof(KableSession<string>).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!;
        var wrapperType = typeof(TrackedChannel<>).MakeGenericType(field.FieldType.GenericTypeArguments);
        var wrapper = Activator.CreateInstance(wrapperType, field.GetValue(session))!;
        field.SetValue(session, wrapper);
        return (ITrackedChannel)wrapper;
    }

    private interface ITrackedChannel
    {
        int Registrations { get; }
        int Pending { get; }
    }

    private sealed class TrackedChannel<T> : Channel<T>, ITrackedChannel
    {
        private readonly TrackedReader _reader;
        public int Registrations => Volatile.Read(ref _reader.Registrations);
        public int Pending => Volatile.Read(ref _reader.Pending);

        public TrackedChannel(Channel<T> inner)
        {
            _reader = new TrackedReader(inner.Reader);
            Reader = _reader;
            Writer = inner.Writer;
        }

        private sealed class TrackedReader(ChannelReader<T> inner) : ChannelReader<T>
        {
            public int Registrations;
            public int Pending;
            public override Task Completion => inner.Completion;
            public override bool TryRead(out T item) => inner.TryRead(out item!);

            public override ValueTask<bool> WaitToReadAsync(CancellationToken ct = default)
            {
                Interlocked.Increment(ref Registrations);
                Interlocked.Increment(ref Pending);
                return ObserveAsync(ct);
            }

            private async ValueTask<bool> ObserveAsync(CancellationToken ct)
            {
                try { return await inner.WaitToReadAsync(ct).ConfigureAwait(false); }
                finally { Interlocked.Decrement(ref Pending); }
            }
        }
    }
}
