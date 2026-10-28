namespace Kable.ProtocolPack.Models;

/// <summary>
/// Connection seed aligned with KableDeviceOptions transport fields.
/// </summary>
public sealed class EndpointSpec
{
    public string Transport { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string? Host { get; set; }

    public int? Port { get; set; }

    public string? PortName { get; set; }

    public int? BaudRate { get; set; }

    public string? Parity { get; set; }

    public int? DataBits { get; set; }

    public string? StopBits { get; set; }

    public string? PipeName { get; set; }

    public string? ServerName { get; set; }
}
