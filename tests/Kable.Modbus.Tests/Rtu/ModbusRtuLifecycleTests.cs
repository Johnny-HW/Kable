namespace Kable.Modbus.Tests.Rtu;

using System.IO.Ports;
using Kable.Exceptions;
using Kable.Modbus.Rtu;
using Xunit;

public sealed class ModbusRtuLifecycleTests
{
    [Fact]
    public async Task QueueTimeoutDoesNotCloseActiveExchange()
    {
        await using var link = await RtuTestLink.OpenAsync();
        var first = link.Master.ReadCoilsAsync(0, 1);
        await link.ReadRequestAsync();
        link.Master.DefaultTimeout = TimeSpan.FromMilliseconds(50);
        var queued = link.Master.ReadCoilsAsync(1, 1);
        await Assert.ThrowsAsync<DeviceTimeoutException>(() => queued);
        Assert.True(link.Master.IsConnected);
        await link.RespondAsync([1, 1, 1, 1]);
        Assert.True((await first)[0]);
    }

    [Fact]
    public async Task CancelBeforeTransmissionPreservesSession()
    {
        await using var link = await RtuTestLink.OpenAsync();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => link.Master.ReadCoilsAsync(0, 1, ct: canceled.Token));
        Assert.True(link.Master.IsConnected);
    }

    [Theory]
    [InlineData(0)] // wrong unit
    [InlineData(1)] // wrong function
    [InlineData(2)] // wrong quantity
    [InlineData(3)] // wrong write acknowledgement
    public async Task MismatchedResponseClosesSession(int fault)
    {
        await using var link = await RtuTestLink.OpenAsync();
        Task pending = fault == 3 ? link.Master.WriteSingleRegisterAsync(0, 1) : link.Master.ReadCoilsAsync(0, 1);
        await link.ReadRequestAsync();
        byte[] body = fault switch
        {
            0 => [2, 1, 1, 1],
            1 => [1, 2, 1, 1],
            2 => [1, 1, 2, 1, 0],
            _ => [1, 6, 0, 0, 0, 2]
        };
        await link.RespondAsync(body);
        await Assert.ThrowsAsync<DeviceDisconnectedException>(() => pending.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.False(link.Master.IsConnected);
    }

    [Fact]
    public async Task DisposeCancelsActiveAndQueuedRequests()
    {
        await using var link = await RtuTestLink.OpenAsync();
        var first = link.Master.ReadCoilsAsync(0, 1);
        await link.ReadRequestAsync();
        var queued = link.Master.ReadCoilsAsync(1, 1);
        await link.Master.DisposeAsync();
        var activeError = await Record.ExceptionAsync(() => first.WaitAsync(TimeSpan.FromSeconds(3)));
        var queuedError = await Record.ExceptionAsync(() => queued.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.True(activeError is OperationCanceledException or DeviceDisconnectedException);
        Assert.True(queuedError is OperationCanceledException or DeviceDisconnectedException);
        Assert.True(first.IsCompleted);
        Assert.True(queued.IsCompleted);
        Assert.False(link.Master.IsConnected);
    }

    [Fact]
    public void InvalidSerialOptionsAreRejectedWithoutOpeningPort()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModbusRtuMaster(new ModbusRtuOptions { BaudRate = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModbusRtuMaster(new ModbusRtuOptions { DataBits = 7 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModbusRtuMaster(new ModbusRtuOptions { DefaultUnitId = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModbusRtuMaster(new ModbusRtuOptions { Timeout = TimeSpan.Zero }));
        Assert.Throws<ArgumentException>(() => new ModbusRtuMaster(new ModbusRtuOptions { Parity = Parity.None, StopBits = StopBits.One }));
        Assert.Throws<ArgumentException>(() => new ModbusRtuMaster(new ModbusRtuOptions { StopBits = StopBits.None }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModbusRtuMaster(new ModbusRtuOptions { InterFrameDelay = TimeSpan.FromMilliseconds(-1) }));
    }
}
