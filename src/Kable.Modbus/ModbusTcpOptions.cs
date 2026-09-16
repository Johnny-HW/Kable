namespace Kable.Modbus;

using System;

public sealed class ModbusTcpOptions
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 502;
    public byte DefaultUnitId { get; set; } = 1;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);
}
