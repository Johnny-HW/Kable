namespace Kable.Modbus.Tests.Rtu;

using System.Diagnostics;
using Kable.Exceptions;
using Kable.Modbus.Rtu;
using Xunit;

public sealed class ModbusRtuMasterTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(15)]
    [InlineData(16)]
    public async Task SupportedOperations_SendRtuFramesAndParseReplies(int function)
    {
        await using var link = await RtuTestLink.OpenAsync();
        var master = link.Master;
        var pending = function switch
        {
            1 => VerifyBits(master.ReadCoilsAsync(0, 9)),
            2 => VerifyBits(master.ReadDiscreteInputsAsync(0, 9)),
            3 => VerifyWords(master.ReadHoldingRegistersAsync(0, 2)),
            4 => VerifyWords(master.ReadInputRegistersAsync(0, 2)),
            5 => master.WriteSingleCoilAsync(0, true),
            6 => master.WriteSingleRegisterAsync(0, 0x1234),
            15 => master.WriteMultipleCoilsAsync(0, new[] { true, false, true }),
            _ => master.WriteMultipleRegistersAsync(0, new ushort[] { 0x1234, 0x5678 })
        };
        var request = await link.ReadRequestAsync();
        Assert.Equal(function, request[1]);
        Assert.Equal(1, request[0]);
        if (function is 1 or 2) await link.RespondAsync([1, (byte)function, 2, 0x85, 1], fragmented: true);
        else if (function is 3 or 4) await link.RespondAsync([1, (byte)function, 4, 0x12, 0x34, 0x56, 0x78]);
        else await link.RespondAsync(request[..6]);
        await pending.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task DeviceException_DoesNotCloseHealthySession()
    {
        await using var link = await RtuTestLink.OpenAsync();
        var pending = link.Master.ReadHoldingRegistersAsync(0, 1);
        await link.ReadRequestAsync();
        await link.RespondAsync([1, 0x83, 2]);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
        Assert.Contains("Code 0x02", error.Message);
        Assert.True(link.Master.IsConnected);
        var next = link.Master.ReadHoldingRegistersAsync(0, 1);
        await link.ReadRequestAsync();
        await link.RespondAsync([1, 3, 2, 0, 7]);
        Assert.Equal(new ushort[] { 7 }, await next);
    }

    [Fact]
    public async Task ConcurrentRequests_AreSerializedAndRespectFrameDelay()
    {
        await using var link = await RtuTestLink.OpenAsync(new ModbusRtuOptions { InterFrameDelay = TimeSpan.FromMilliseconds(50) });
        var first = link.Master.ReadHoldingRegistersAsync(0, 1);
        var second = link.Master.ReadHoldingRegistersAsync(1, 1);
        await link.ReadRequestAsync();
        Assert.False(second.IsCompleted);
        long sent = Stopwatch.GetTimestamp();
        await link.RespondAsync([1, 3, 2, 0, 1]);
        await link.ReadRequestAsync();
        Assert.True(Stopwatch.GetElapsedTime(sent) >= TimeSpan.FromMilliseconds(45));
        await link.RespondAsync([1, 3, 2, 0, 2]);
        Assert.Equal(new ushort[] { 1 }, await first);
        Assert.Equal(new ushort[] { 2 }, await second);
    }

    [Fact]
    public async Task TimeoutAfterSend_ClosesSessionAndRejectsReuse()
    {
        await using var link = await RtuTestLink.OpenAsync(new ModbusRtuOptions { Timeout = TimeSpan.FromMilliseconds(100) });
        var pending = link.Master.ReadCoilsAsync(0, 1);
        await link.ReadRequestAsync();
        await Assert.ThrowsAsync<DeviceTimeoutException>(() => pending);
        Assert.False(link.Master.IsConnected);
        await Assert.ThrowsAsync<DeviceDisconnectedException>(() => link.Master.ReadCoilsAsync(0, 1));
    }

    [Fact]
    public async Task CancelAfterSend_ClosesSession()
    {
        await using var link = await RtuTestLink.OpenAsync();
        using var cancel = new CancellationTokenSource();
        var pending = link.Master.ReadCoilsAsync(0, 1, ct: cancel.Token);
        await link.ReadRequestAsync();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.False(link.Master.IsConnected);
    }

    [Fact]
    public async Task CancelWhileQueued_DoesNotCloseSession()
    {
        await using var link = await RtuTestLink.OpenAsync();
        var first = link.Master.ReadCoilsAsync(0, 1);
        await link.ReadRequestAsync();
        using var cancel = new CancellationTokenSource();
        var second = link.Master.ReadCoilsAsync(1, 1, ct: cancel.Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        await link.RespondAsync([1, 1, 1, 1]);
        Assert.True((await first)[0]);
        Assert.True(link.Master.IsConnected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(248)]
    public async Task InvalidUnitIsRejectedBeforeSend(byte unit)
    {
        await using var link = await RtuTestLink.OpenAsync();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => link.Master.ReadCoilsAsync(0, 1, unit));
        Assert.True(link.Master.IsConnected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CrcFailureOrDisconnect_FailsPendingRequest(bool corrupt)
    {
        await using var link = await RtuTestLink.OpenAsync();
        var pending = link.Master.ReadCoilsAsync(0, 1);
        await link.ReadRequestAsync();
        if (corrupt) await link.RespondAsync([1, 1, 1, 1], corrupt: true);
        else await link.DisconnectAsync();
        await Assert.ThrowsAsync<DeviceDisconnectedException>(() => pending.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.False(link.Master.IsConnected);
    }

    private static async Task VerifyBits(Task<bool[]> operation) =>
        Assert.Equal(new[] { true, false, true, false, false, false, false, true, true }, await operation);
    private static async Task VerifyWords(Task<ushort[]> operation) =>
        Assert.Equal(new ushort[] { 0x1234, 0x5678 }, await operation);
}
