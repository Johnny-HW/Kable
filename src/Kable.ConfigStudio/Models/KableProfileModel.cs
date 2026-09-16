namespace Kable.ConfigStudio.Models;

public enum TransportType
{
    Serial,
    Tcp,
    NamedPipe
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
    public string Description { get; set; } = "Chamber 1 약액 공급 펌프 통신 채널";

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
