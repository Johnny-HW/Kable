namespace Kable.Modbus.Tests.Data;

using Kable.Modbus.Data;
using Kable.Modbus.Tests.Coils;
using Xunit;

public sealed class PlcTagClientTests
{
    [Fact]
    public async Task ReadsAddressOrderScaleAndUnit()
    {
        var session = new ReplySession(r => new(r.TransactionId, r.UnitId, r.FunctionCode, new byte[] { 4, 0, 0, 0x41, 0x48 }));
        await using var master = new ModbusTcpMaster(session, new ModbusTcpOptions());
        var tag = new PlcTag("temperature", 100, PlcValueKind.Single, PlcDataOrder.WordSwap, Scale: 2, Offset: -5, Unit: "C");
        var result = await new PlcTagClient(master).ReadAsync(tag);
        Assert.Equal(12.5f, result.RawValue);
        Assert.Equal(20d, result.Value);
        Assert.Equal("C", result.Tag.Unit);
        Assert.Equal(new byte[] { 0, 100, 0, 2 }, session.Request!.Payload.ToArray());
    }

    [Fact]
    public async Task WritesInverseScalingAndRejectsFractionalInteger()
    {
        var session = new ReplySession(r => new(r.TransactionId, r.UnitId, r.FunctionCode, r.Payload[..4]));
        await using var master = new ModbusTcpMaster(session, new ModbusTcpOptions());
        var client = new PlcTagClient(master);
        var tag = new PlcTag("speed", 10, PlcValueKind.UInt16, Scale: 0.1, Unit: "rpm");
        await client.WriteAsync(tag, 12.5);
        Assert.Equal(new byte[] { 0, 10, 0, 1, 2, 0, 125 }, session.Request!.Payload.ToArray());
        await Assert.ThrowsAsync<ArgumentException>(() => client.WriteAsync(tag, 12.55));
    }

    [Fact]
    public async Task AsciiAndInputRegisterContracts()
    {
        var session = new ReplySession(r => new(r.TransactionId, r.UnitId, r.FunctionCode, new byte[] { 4, 65, 66, 67, 0 }));
        await using var master = new ModbusTcpMaster(session, new ModbusTcpOptions());
        var client = new PlcTagClient(master);
        var tag = new PlcTag("model", 0, PlcValueKind.Ascii, StringRegisters: 2, Area: PlcRegisterArea.Input);
        Assert.Equal("ABC", (await client.ReadAsync(tag)).Value);
        Assert.Equal(4, session.Request!.FunctionCode);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.WriteAsync(tag, "X"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ReadAsync(tag with { Scale = 0 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ReadAsync(tag with { Address = 65535 }));
    }
}
