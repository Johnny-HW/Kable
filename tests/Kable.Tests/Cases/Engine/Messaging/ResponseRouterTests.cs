namespace Kable.Tests.Cases.Engine.Messaging;

using Kable.Codecs;
using Kable.Engine.Messaging;
using Xunit;

public sealed class ResponseRouterTests
{
    [Fact]
    public async Task Fifo_routes_one_reply_and_fails_pending_and_future_reservations_on_disconnect()
    {
        using var router = new ResponseRouter<string>(new AsciiLineCodec());
        await router.WaitForFifoAsync(default);
        var first = router.BeginFifo();
        Assert.True(router.TryRoute("reply"));
        Assert.Equal("reply", await first.Task);
        Assert.False(router.TryRoute("unsolicited"));
        router.EndFifo();
        var pending = router.Reserve("one");
        var reason = new IOException("closed");
        router.FailAll(reason);
        Assert.Same(reason, await Assert.ThrowsAsync<IOException>(() => pending.Task));
        var future = router.Reserve("two");
        Assert.Same(reason, await Assert.ThrowsAsync<IOException>(() => future.Task));
    }

    [Fact]
    public void Old_cid_owner_cannot_remove_a_new_reservation()
    {
        using var router = new ResponseRouter<string>(new AsciiLineCodec());
        var first = router.Reserve("cid");
        Assert.Throws<InvalidOperationException>(() => router.Reserve("cid"));
        router.Release("cid", first);
        var second = router.Reserve("cid");
        router.Release("cid", first);
        Assert.Throws<InvalidOperationException>(() => router.Reserve("cid"));
        router.Release("cid", second);
        router.Reserve("cid");
    }
}
