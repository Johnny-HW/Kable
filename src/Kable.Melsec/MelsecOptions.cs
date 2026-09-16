namespace Kable.Melsec;

using System;

public sealed class MelsecOptions
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5000;
    public byte NetworkNo { get; set; } = 0;
    public byte PcNo { get; set; } = 0xFF;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);
}
