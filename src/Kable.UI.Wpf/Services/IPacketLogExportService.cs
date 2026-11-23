namespace Kable.UI.Wpf.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Kable.UI.Wpf.ViewModels;
using Microsoft.Win32;

public interface IPacketLogExportService
{
    string? ExportPacketsWithDialog(
        IEnumerable<PacketDisplayModel> packets,
        string dialogTitle,
        string defaultFilePrefix,
        string logHeaderTitle);
}

public sealed class WpfPacketLogExportService : IPacketLogExportService
{
    public static WpfPacketLogExportService Instance { get; } = new();

    public string? ExportPacketsWithDialog(
        IEnumerable<PacketDisplayModel> packets,
        string dialogTitle,
        string defaultFilePrefix,
        string logHeaderTitle)
    {
        ArgumentNullException.ThrowIfNull(packets);

        var dialog = new SaveFileDialog
        {
            Title = dialogTitle,
            Filter = "Log Files (*.log)|*.log|CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            FileName = $"{defaultFilePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.log"
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# {logHeaderTitle}");
        sb.AppendLine($"# Exported at: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine("# Seq | Time | Direction | Device | Kind | Length | Latency | Payload");
        sb.AppendLine(new string('-', 90));

        foreach (var pkt in packets)
        {
            sb.AppendLine($"[{pkt.SequenceNo,5}] {pkt.Timestamp} | {pkt.Direction} | {pkt.DeviceId,-12} | {pkt.Kind,-18} | {pkt.Length,4}B | {pkt.LatencyMs,7} | {pkt.AsciiPreview}");
        }

        File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
        return dialog.FileName;
    }
}
