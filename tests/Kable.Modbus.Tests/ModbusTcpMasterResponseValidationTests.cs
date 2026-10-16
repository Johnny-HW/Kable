namespace Kable.Modbus.Tests;

using Kable.Engine;
using Kable.Modbus.Messages;
using Kable.Observability;
using Xunit;

public sealed class ModbusTcpMasterResponseValidationTests
{
    public static TheoryData<byte[]> InvalidBitPayloads => new()
    {
        Array.Empty<byte>(), new byte[] { 2, 0xFF }, new byte[] { 1, 0xFF },
        new byte[] { 2, 0xFF, 0xFF, 0 }, new byte[] { 3, 0xFF, 0xFF, 0xFF },
        new byte[] { 0 },
    };

    public static TheoryData<byte, byte[]> InvalidRegisterPayloads
    {
        get
        {
            var cases = new TheoryData<byte, byte[]>();
            byte[][] payloads =
            [
                [], [4, 0, 1], [2, 0, 1], [3, 0, 1, 2],
                [4, 0, 1, 0, 2, 0], [6, 0, 1, 0, 2, 0, 3], [0],
            ];
            foreach (byte function in new byte[] { 3, 4 })
                foreach (var payload in payloads)
                    cases.Add(function, payload);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(InvalidBitPayloads))]
    public async Task ReadDiscreteInputs_RejectsMalformedPayload(byte[] payload)
    {
        await using var master = CreateMaster(payload);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => master.ReadDiscreteInputsAsync(0, 16));
    }

    [Theory]
    [MemberData(nameof(InvalidRegisterPayloads))]
    public async Task ReadRegisters_RejectsMalformedPayload(byte function, byte[] payload)
    {
        await using var master = CreateMaster(payload);
        await Assert.ThrowsAsync<InvalidOperationException>(() => function == 3
            ? master.ReadHoldingRegistersAsync(0, 2)
            : master.ReadInputRegistersAsync(0, 2));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(16)]
    public async Task ReadDiscreteInputs_ReturnsOnlyRequestedBits(ushort count)
    {
        var payload = Enumerable.Repeat((byte)0xFF, 1 + (count + 7) / 8).ToArray();
        payload[0] = (byte)(payload.Length - 1);
        await using var master = CreateMaster(payload);
        var bits = await master.ReadDiscreteInputsAsync(0, count);
        Assert.Equal(count, bits.Length);
        Assert.All(bits, bit => Assert.True(bit));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ReadRegisters_PreservesBigEndianValues(byte function)
    {
        await using var master = CreateMaster([4, 0x12, 0x34, 0xAB, 0xCD]);
        var result = function == 3
            ? await master.ReadHoldingRegistersAsync(0, 2)
            : await master.ReadInputRegistersAsync(0, 2);
        Assert.Equal(new ushort[] { 0x1234, 0xABCD }, result);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Read_PreservesDeviceException(byte function)
    {
        await using var master = new ModbusTcpMaster(new ResponseSession([2], true), new ModbusTcpOptions());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            if (function == 2) await master.ReadDiscreteInputsAsync(0, 16);
            else if (function == 3) await master.ReadHoldingRegistersAsync(0, 2);
            else await master.ReadInputRegistersAsync(0, 2);
        });
        Assert.Contains("Code 0x02", error.Message);
    }

    private static ModbusTcpMaster CreateMaster(byte[] payload) =>
        new(new ResponseSession(payload), new ModbusTcpOptions());

    private sealed class ResponseSession(byte[] payload, bool exception = false) : IDeviceSession<ModbusTcpMessage>
    {
        public bool IsConnected => true;
        public CommHealthMetrics HealthMetrics => throw new NotSupportedException();
        public IAsyncEnumerable<ModbusTcpMessage> Stream => throw new NotSupportedException();
        public ValueTask<TResponse> RequestAsync<TResponse>(ModbusTcpMessage request, TimeSpan timeout, CancellationToken ct = default)
        {
            var response = new ModbusTcpMessage(request.TransactionId, request.UnitId,
                exception ? (byte)(request.FunctionCode | 0x80) : request.FunctionCode, payload);
            return new ValueTask<TResponse>((TResponse)(object)response);
        }
        public ValueTask StartAsync(CancellationToken ct = default) => default;
        public ValueTask StopAsync() => default;
        public ValueTask DisposeAsync() => default;
        public void Dispose() { }
        public ValueTask SendAsync(ModbusTcpMessage message, CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask SendUrgentAsync(ModbusTcpMessage message) => throw new NotSupportedException();
    }
}
