namespace Kable.UI.Wpf.Models;

public enum TransportType
{
    Serial,
    Tcp,
    NamedPipe,
    ModbusTcp,
    MelsecSlmp,
    Mqtt,
    OpcUa
}

public enum CodecType
{
    AsciiLine,
    ModbusRtu,
    ByteLengthPrefix,
    Raw
}

public enum RouterType
{
    FifoLock,
    CorrelationIdMultiplex
}

public sealed class KableProfileModel
{
    public string DeviceName { get; set; } = "Chemical_Pump_Ch1";
    public string Description { get; set; } = "Chamber 1 Chemical Supply Pump Comm Channel";

    // Transport
    public TransportType Transport { get; set; } = TransportType.Serial;
    
    // Serial Options
    public string PortName { get; set; } = "COM3";
    public int BaudRate { get; set; } = 19200;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";

    // TCP Options
    public string HostIp { get; set; } = "192.168.0.100";
    public int TcpPort { get; set; } = 502;

    // Modbus-TCP Options
    public byte ModbusUnitId { get; set; } = 1;
    public ushort ModbusTestRegister { get; set; } = 100;

    // Melsec SLMP Options
    public byte MelsecNetworkNo { get; set; } = 0;
    public byte MelsecPcNo { get; set; } = 255;
    public string MelsecDevice { get; set; } = "D1000";

    // MQTT Options
    public string MqttClientId { get; set; } = "Kable_Studio_Tester";
    public string MqttTopicPrefix { get; set; } = "kable/telemetry";

    // OPC UA Options
    public string OpcEndpointUrl { get; set; } = "opc.tcp://192.168.0.100:4840";
    public string OpcNodeId { get; set; } = "ns=2;s=Device.Status";

    // NamedPipe Options
    public string PipeName { get; set; } = "kable_pump_ch1";

    // Codec & Protocol
    public CodecType Codec { get; set; } = CodecType.ModbusRtu;
    public string DelimiterHex { get; set; } = "0A";

    // Router
    public RouterType Router { get; set; } = RouterType.FifoLock;

    // Timeout & Fail-Fast
    public int TimeoutMs { get; set; } = 1000;
    public int HeartbeatIntervalMs { get; set; } = 5000;
}
