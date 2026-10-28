namespace Kable.Modbus.Rtu;

using Kable.Core;
using Kable.Engine;
using Kable.Modbus.Codecs;
using Kable.Modbus.Messages;
using Kable.Transports;

/// <summary>Single-bus RTU master. Owns its session; never retries or broadcasts commands.</summary>
public sealed partial class ModbusRtuMaster : IModbusCoilMaster
{
    private readonly ModbusTcpMaster _operations;
    private readonly ModbusRtuRequestSession _session;
    public bool IsConnected => _session.IsConnected;
    public byte DefaultUnitId
    {
        get => _operations.DefaultUnitId;
        set { ModbusRtuOptions.ValidateUnit(value); _operations.DefaultUnitId = value; }
    }
    public TimeSpan DefaultTimeout
    {
        get => _operations.DefaultTimeout;
        set { ModbusRtuOptions.ValidateTimeout(value); _operations.DefaultTimeout = value; }
    }

    public ModbusRtuMaster(ModbusRtuOptions options) : this(CreateSerialFactory(options), options) { }

    /// <summary>Accepts a byte-stream factory for adapters and hardware-free testing. The master owns the connection.</summary>
    public ModbusRtuMaster(IConnectionFactory factory, ModbusRtuOptions options)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);
        var delay = options.GetFrameDelay();
        _session = new ModbusRtuRequestSession(new KableSession<ModbusTcpMessage>(factory, new ModbusRtuMasterCodec()), delay);
        // Command/PDU semantics are identical to TCP; the RTU codec supplies address and CRC instead of MBAP.
        _operations = new ModbusTcpMaster(_session, new ModbusTcpOptions { DefaultUnitId = options.DefaultUnitId, Timeout = options.Timeout });
    }

    private static IConnectionFactory CreateSerialFactory(ModbusRtuOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.GetFrameDelay();
        return new SerialPortConnectionFactory(options.PortName, options.BaudRate, options.Parity, options.DataBits, options.StopBits);
    }
    public Task StartAsync(CancellationToken ct = default) => _operations.StartAsync(ct);
    public ValueTask DisposeAsync() => _operations.DisposeAsync();
}
