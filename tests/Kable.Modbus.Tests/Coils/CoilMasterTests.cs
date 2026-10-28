namespace Kable.Modbus.Tests.Coils;

using Kable.Engine;
using Kable.Modbus.Messages;
using Kable.Observability;
using Xunit;

public sealed class CoilMasterTests
{
    [Fact]
    public async Task ReadCoils_UnpacksLsbFirstAndTrimsPadding()
    {
        var session = new ReplySession(r => new(r.TransactionId, r.UnitId, r.FunctionCode, new byte[] { 2, 0x85, 0xFF }));
        await using var master = new ModbusTcpMaster(session, new ModbusTcpOptions());
        IModbusCoilMaster api = master;
        var result = await api.ReadCoilsAsync(10, 9);
        Assert.Equal(new[] { true, false, true, false, false, false, false, true, true }, result);
        Assert.Equal(1, session.Request!.FunctionCode);
        Assert.Equal(new byte[] { 0, 10, 0, 9 }, session.Request.Payload.ToArray());
    }

    [Fact]
    public async Task WriteCoils_PacksBitsAndValidatesEcho()
    {
        var session = new ReplySession(r => new(r.TransactionId, r.UnitId, r.FunctionCode, r.Payload[..4]));
        await using var master = new ModbusTcpMaster(session, new ModbusTcpOptions());
        await master.WriteMultipleCoilsAsync(20, new[] { true, false, true, false, false, false, false, true, true });
        Assert.Equal(15, session.Request!.FunctionCode);
        Assert.Equal(new byte[] { 0, 20, 0, 9, 2, 0x85, 1 }, session.Request.Payload.ToArray());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 2001)]
    [InlineData(65535, 2)]
    public async Task ReadCoils_RejectsInvalidRangeBeforeSending(ushort address, ushort count)
    {
        var session = new ReplySession(r => r);
        await using var master = new ModbusTcpMaster(session, new ModbusTcpOptions());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => master.ReadCoilsAsync(address, count));
        Assert.Null(session.Request);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1969)]
    public async Task WriteCoils_RejectsInvalidQuantity(int count)
    {
        var session = new ReplySession(r => r);
        await using var master = new ModbusTcpMaster(session, new ModbusTcpOptions());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => master.WriteMultipleCoilsAsync(0, new bool[count]));
        Assert.Null(session.Request);
    }

    [Theory]
    [InlineData(0)] // unit
    [InlineData(1)] // function
    [InlineData(2)] // protocol
    [InlineData(3)] // byte count
    public async Task ReadCoils_RejectsWrongResponse(int fault)
    {
        var session = new ReplySession(r => new(r.TransactionId,
            fault == 0 ? (byte)2 : r.UnitId, fault == 1 ? (byte)2 : r.FunctionCode,
            fault == 3 ? new byte[] { 2, 1 } : new byte[] { 1, 1 }, fault == 2 ? (ushort)1 : (ushort)0));
        await using var master = new ModbusTcpMaster(session, new ModbusTcpOptions());
        await Assert.ThrowsAsync<InvalidOperationException>(() => master.ReadCoilsAsync(0, 1));
    }

    [Fact]
    public async Task WriteCoils_RejectsWrongEcho()
    {
        await using var master = new ModbusTcpMaster(new ReplySession(r => new(r.TransactionId, r.UnitId,
            r.FunctionCode, new byte[] { 0, 1, 0, 1 })), new ModbusTcpOptions());
        await Assert.ThrowsAsync<InvalidOperationException>(() => master.WriteMultipleCoilsAsync(0, new[] { true }));
    }

    [Fact]
    public async Task ReadCoils_PreservesDeviceException()
    {
        await using var master = new ModbusTcpMaster(new ReplySession(r => new(r.TransactionId, r.UnitId,
            0x81, new byte[] { 2 })), new ModbusTcpOptions());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => master.ReadCoilsAsync(0, 1));
        Assert.Contains("Code 0x02", error.Message);
    }

    [Fact]
    public void MessageFactories_AcceptLastAddressAndMaximumQuantities()
    {
        Assert.Equal(4, ModbusTcpMessage.CreateReadCoils(1, 1, 65535, 1).Payload.Length);
        Assert.Equal(251, ModbusTcpMessage.CreateWriteMultipleCoils(1, 1, 0, new bool[1968]).Payload.Length);
    }
}

internal sealed class ReplySession(Func<ModbusTcpMessage, ModbusTcpMessage> reply) : IDeviceSession<ModbusTcpMessage>
{
    public ModbusTcpMessage? Request { get; private set; }
    public bool IsConnected => true;
    public CommHealthMetrics HealthMetrics => throw new NotSupportedException();
    public IAsyncEnumerable<ModbusTcpMessage> Stream => throw new NotSupportedException();
    public ValueTask<TResponse> RequestAsync<TResponse>(ModbusTcpMessage request, TimeSpan timeout, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Request = request;
        return new((TResponse)(object)reply(request));
    }
    public ValueTask StartAsync(CancellationToken ct = default) => default;
    public ValueTask StopAsync() => default;
    public ValueTask DisposeAsync() => default;
    public void Dispose() { }
    public ValueTask SendAsync(ModbusTcpMessage message, CancellationToken ct = default) => throw new NotSupportedException();
    public ValueTask SendUrgentAsync(ModbusTcpMessage message) => throw new NotSupportedException();
}
