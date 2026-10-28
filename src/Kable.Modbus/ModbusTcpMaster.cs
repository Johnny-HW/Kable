namespace Kable.Modbus;

using System;
using System.Buffers.Binary;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kable.Core;
using Kable.Engine;
using Kable.Modbus.Codecs;
using Kable.Modbus.Messages;
using Kable.Modbus.Protocol;

/// <summary>
/// KableSession 기반의 고수준 Modbus-TCP 마스터 클라이언트.
/// 자동 Transaction ID 발급, 동시 다중 비동기 질의(Pipelining), 에러 감지를 제공합니다.
/// </summary>
public sealed partial class ModbusTcpMaster : IModbusCoilMaster
{
    private readonly IDeviceSession<ModbusTcpMessage> _session;
    private readonly ILogger<ModbusTcpMaster>? _logger;
    private int _nextTransactionId;

    public byte DefaultUnitId { get; set; }
    public TimeSpan DefaultTimeout { get; set; }

    public ModbusTcpMaster(
        IDeviceSession<ModbusTcpMessage> session,
        IOptions<ModbusTcpOptions> options,
        ILogger<ModbusTcpMaster>? logger = null)
        : this(session, (options ?? throw new ArgumentNullException(nameof(options))).Value, logger)
    {
    }

    public ModbusTcpMaster(
        IDeviceSession<ModbusTcpMessage> session,
        ModbusTcpOptions options,
        ILogger<ModbusTcpMaster>? logger = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;

        DefaultUnitId = options.DefaultUnitId;
        DefaultTimeout = options.Timeout;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        await _session.StartAsync(ct).ConfigureAwait(false);
    }

    private ushort GetNextTransactionId()
    {
        return (ushort)(Interlocked.Increment(ref _nextTransactionId) & 0xFFFF);
    }


    /// <summary>
    /// FC02 Read Discrete Inputs 실행
    /// </summary>
    public async Task<bool[]> ReadDiscreteInputsAsync(ushort startAddress, ushort count, byte? unitId = null, CancellationToken ct = default)
    {
        ModbusValidation.Range(startAddress, count, 2000);
        ushort transId = GetNextTransactionId();
        byte targetUnit = unitId ?? DefaultUnitId;
        var request = ModbusTcpMessage.CreateReadDiscreteInputs(transId, targetUnit, startAddress, count);

        var response = await _session.RequestAsync<ModbusTcpMessage>(request, DefaultTimeout, ct).ConfigureAwait(false);

        ModbusValidation.Response(request, response);

        var span = response.Payload.Span;
        ValidateReadPayload(span, (count + 7) / 8);
        var bits = new bool[count];
        for (int i = 0; i < count; i++)
        {
            int byteIdx = 1 + (i / 8);
            int bitIdx = i % 8;
            bits[i] = (span[byteIdx] & (1 << bitIdx)) != 0;
        }

        return bits;
    }

    /// <summary>
    /// FC04 Read Input Registers 실행
    /// </summary>
    public async Task<ushort[]> ReadInputRegistersAsync(ushort startAddress, ushort count, byte? unitId = null, CancellationToken ct = default)
    {
        ModbusValidation.Range(startAddress, count, 125);
        ushort transId = GetNextTransactionId();
        byte targetUnit = unitId ?? DefaultUnitId;
        var request = ModbusTcpMessage.CreateReadRegisters(transId, targetUnit, 0x04, startAddress, count);

        var response = await _session.RequestAsync<ModbusTcpMessage>(request, DefaultTimeout, ct).ConfigureAwait(false);

        ModbusValidation.Response(request, response);

        var span = response.Payload.Span;
        ValidateReadPayload(span, count * 2);
        int registerCount = count;
        var registers = new ushort[registerCount];

        for (int i = 0; i < registerCount; i++)
        {
            registers[i] = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(1 + (i * 2), 2));
        }

        return registers;
    }

    /// <summary>
    /// FC05 Write Single Coil 실행
    /// </summary>
    public async Task WriteSingleCoilAsync(ushort address, bool value, byte? unitId = null, CancellationToken ct = default)
    {
        ushort transId = GetNextTransactionId();
        byte targetUnit = unitId ?? DefaultUnitId;
        var request = ModbusTcpMessage.CreateWriteSingleCoil(transId, targetUnit, address, value);

        var response = await _session.RequestAsync<ModbusTcpMessage>(request, DefaultTimeout, ct).ConfigureAwait(false);

        ModbusValidation.Response(request, response);
        ModbusValidation.WriteEcho(request, response);
    }

    /// <summary>
    /// FC03 Read Holding Registers 실행
    /// </summary>
    public async Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort count, byte? unitId = null, CancellationToken ct = default)
    {
        ModbusValidation.Range(startAddress, count, 125);
        ushort transId = GetNextTransactionId();
        byte targetUnit = unitId ?? DefaultUnitId;
        var request = ModbusTcpMessage.CreateReadRegisters(transId, targetUnit, 0x03, startAddress, count);

        var response = await _session.RequestAsync<ModbusTcpMessage>(request, DefaultTimeout, ct).ConfigureAwait(false);

        ModbusValidation.Response(request, response);

        // Response Payload: ByteCount(1) + RegisterValues(N * 2)
        var span = response.Payload.Span;
        ValidateReadPayload(span, count * 2);
        int registerCount = count;
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

        ModbusValidation.Response(request, response);
        ModbusValidation.WriteEcho(request, response);
    }

    /// <summary>
    /// FC16 Write Multiple Registers 실행
    /// </summary>
    public async Task WriteMultipleRegistersAsync(ushort startAddress, ushort[] values, byte? unitId = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ModbusValidation.Range(startAddress, values.Length, 123);
        ushort transId = GetNextTransactionId();
        byte targetUnit = unitId ?? DefaultUnitId;
        var request = ModbusTcpMessage.CreateWriteMultipleRegisters(transId, targetUnit, startAddress, values);

        var response = await _session.RequestAsync<ModbusTcpMessage>(request, DefaultTimeout, ct).ConfigureAwait(false);

        ModbusValidation.Response(request, response);
        ModbusValidation.WriteEcho(request, response);
    }

    private static void ValidateReadPayload(ReadOnlySpan<byte> payload, int expectedByteCount)
    {
        if (payload.Length != 1 + expectedByteCount || payload[0] != expectedByteCount)
        {
            throw new InvalidOperationException("Modbus response byte count or payload length does not match the requested quantity.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _session.DisposeAsync().ConfigureAwait(false);
    }
}
