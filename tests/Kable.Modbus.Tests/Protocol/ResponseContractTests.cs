namespace Kable.Modbus.Tests.Protocol;

using Kable.Modbus.Tests.Coils;
using Xunit;

public sealed class ResponseContractTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(16)]
    public async Task ExistingWrites_RejectMismatchedEcho(int function)
    {
        await using var master = new ModbusTcpMaster(new ReplySession(r => new(r.TransactionId, r.UnitId,
            r.FunctionCode, new byte[] { 0, 5, 0, 99 })), new ModbusTcpOptions());
        await Assert.ThrowsAsync<InvalidOperationException>(() => function switch
        {
            5 => master.WriteSingleCoilAsync(0, true),
            6 => master.WriteSingleRegisterAsync(0, 1),
            _ => master.WriteMultipleRegistersAsync(0, new ushort[] { 1 })
        });
    }

    [Theory]
    [InlineData(2, 2001)]
    [InlineData(3, 126)]
    [InlineData(4, 126)]
    [InlineData(16, 124)]
    public async Task ExistingOperations_RejectOversizedQuantityBeforeSending(int function, ushort count)
    {
        var session = new ReplySession(r => r);
        await using var master = new ModbusTcpMaster(session, new ModbusTcpOptions());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            switch (function)
            {
                case 2: await master.ReadDiscreteInputsAsync(0, count); break;
                case 3: await master.ReadHoldingRegistersAsync(0, count); break;
                case 4: await master.ReadInputRegistersAsync(0, count); break;
                default: await master.WriteMultipleRegistersAsync(0, new ushort[count]); break;
            }
        });
        Assert.Null(session.Request);
    }
}
