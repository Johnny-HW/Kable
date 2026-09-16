namespace Kable.Modbus;

using System;
using System.Buffers.Binary;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;
using Kable.Engine;
using Kable.Modbus.Codecs;
using Kable.Modbus.Messages;

/// <summary>
/// KableSession 기반의 고수준 Modbus-TCP 마스터 클라이언트.
/// 자동 Transaction ID 발급, 동시 다중 비동기 질의(Pipelining), 에러 감지를 제공합니다.
/// </summary>
public sealed class ModbusTcpMaster : IAsyncDisposable
{
    private readonly IDeviceSession<ModbusTcpMessage> _session;
    private int _nextTransactionId;

    public byte DefaultUnitId { get; set; } = 1;
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(3);

    public ModbusTcpMaster(IDeviceSession<ModbusTcpMessage> session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    private ushort GetNextTransactionId()
    {
        return (ushort)(Interlocked.Increment(ref _nextTransactionId) & 0xFFFF);
    }

    /// <summary>
    /// FC03 Read Holding Registers 실행
    /// </summary>
    public async Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort count, byte? unitId = null, CancellationToken ct = default)
    {
        ushort transId = GetNextTransactionId();
        byte targetUnit = unitId ?? DefaultUnitId;
        var request = ModbusTcpMessage.CreateReadRegisters(transId, targetUnit, 0x03, startAddress, count);

        var response = await _session.RequestAsync<ModbusTcpMessage>(request, DefaultTimeout, ct).ConfigureAwait(false);

        if (response.IsException)
        {
            throw new InvalidOperationException($"Modbus Exception Response: Code 0x{response.ExceptionCode:X2}");
        }

        // Response Payload: ByteCount(1) + RegisterValues(N * 2)
        var span = response.Payload.Span;
        if (span.Length < 1) throw new InvalidOperationException("Invalid Modbus response length.");

        byte byteCount = span[0];
        int registerCount = byteCount / 2;
        var registers = new ushort[registerCount];

        for (int i = 0; i < registerCount; i++)
        {
            registers[i] = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(1 + (i * 2), 2));
        }

        return registers;
    }

    /// <summary>
    /// FC06 Write Single Register 실행
    /// </summary>
    public async Task WriteSingleRegisterAsync(ushort address, ushort value, byte? unitId = null, CancellationToken ct = default)
    {
        ushort transId = GetNextTransactionId();
        byte targetUnit = unitId ?? DefaultUnitId;
        var request = ModbusTcpMessage.CreateWriteSingleRegister(transId, targetUnit, address, value);

        var response = await _session.RequestAsync<ModbusTcpMessage>(request, DefaultTimeout, ct).ConfigureAwait(false);

        if (response.IsException)
        {
            throw new InvalidOperationException($"Modbus Exception Response: Code 0x{response.ExceptionCode:X2}");
        }
    }

    /// <summary>
    /// FC16 Write Multiple Registers 실행
    /// </summary>
    public async Task WriteMultipleRegistersAsync(ushort startAddress, ushort[] values, byte? unitId = null, CancellationToken ct = default)
    {
        ushort transId = GetNextTransactionId();
        byte targetUnit = unitId ?? DefaultUnitId;
        var request = ModbusTcpMessage.CreateWriteMultipleRegisters(transId, targetUnit, startAddress, values);

        var response = await _session.RequestAsync<ModbusTcpMessage>(request, DefaultTimeout, ct).ConfigureAwait(false);

        if (response.IsException)
        {
            throw new InvalidOperationException($"Modbus Exception Response: Code 0x{response.ExceptionCode:X2}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _session.DisposeAsync().ConfigureAwait(false);
    }
}
