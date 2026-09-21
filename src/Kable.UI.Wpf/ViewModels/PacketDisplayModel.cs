namespace Kable.UI.Wpf.ViewModels;

public sealed class PacketDisplayModel
{
    public long SequenceNo { get; set; }
    public string Timestamp { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int Length { get; set; }
    public string AsciiPreview { get; set; } = string.Empty;
    public string HexDump { get; set; } = string.Empty;
    public string LatencyMs { get; set; } = string.Empty;
}
