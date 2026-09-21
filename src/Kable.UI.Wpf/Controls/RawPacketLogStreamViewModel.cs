namespace Kable.UI.Wpf.Controls;

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kable.Core;
using Kable.Observability;
using Kable.UI.Wpf.ViewModels;
using Microsoft.Win32;

public partial class RawPacketLogStreamViewModel : ObservableObject, ICommObserver
{
    private readonly Dispatcher _dispatcher;
    private readonly int _maxLogCount;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private PacketDisplayModel? _selectedPacket;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public ObservableCollection<PacketDisplayModel> Packets { get; } = new();

    public ChannelReader<PacketTraceRecord> CommandStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> PeriodicStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> AlarmStream => throw new NotSupportedException();

    public RawPacketLogStreamViewModel(Dispatcher? dispatcher = null, int maxLogCount = 1000)
    {
        _dispatcher = dispatcher ?? (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
        _maxLogCount = maxLogCount;
    }

    public void OnPacketTrace(in PacketTraceRecord trace)
    {
        if (IsPaused) return;

        var raw = trace.RawBytes.ToArray();
        string ascii = trace.ParsedText ?? Encoding.ASCII.GetString(raw).Replace("\r", "\\r").Replace("\n", "\\n");

        if (!string.IsNullOrEmpty(FilterText))
        {
            if (!ascii.Contains(FilterText, StringComparison.OrdinalIgnoreCase) &&
                !trace.DeviceId.Contains(FilterText, StringComparison.OrdinalIgnoreCase) &&
                !trace.Tag.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        var model = new PacketDisplayModel
        {
            SequenceNo = trace.SequenceNo,
            Timestamp = trace.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff"),
            Direction = trace.Direction == PacketDirection.Tx ? "TX ➡" : "RX ⬅",
            DeviceId = trace.DeviceId,
            Kind = trace.Tag.Length > 0 ? trace.Tag : trace.Kind.ToString(),
            Length = raw.Length,
            AsciiPreview = ascii,
            HexDump = HexDumpFormatter.Format(raw),
            LatencyMs = trace.Latency > TimeSpan.Zero ? $"{trace.Latency.TotalMilliseconds:F1} ms" : "-"
        };

        _dispatcher.BeginInvoke(() =>
        {
            if (Packets.Count >= _maxLogCount)
            {
                Packets.RemoveAt(0);
            }
            Packets.Add(model);
        });
    }

    [RelayCommand]
    public void ClearLogs()
    {
        Packets.Clear();
        SelectedPacket = null;
        StatusMessage = "Logs cleared";
    }

    [RelayCommand]
    public void TogglePause()
    {
        IsPaused = !IsPaused;
        StatusMessage = IsPaused ? "Streaming Paused" : "Streaming Active";
    }

    [RelayCommand]
    public void ExportToFile()
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export Packet Stream Logs",
                Filter = "Log Files (*.log)|*.log|CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = $"Kable_PacketStream_{DateTime.Now:yyyyMMdd_HHmmss}.log"
            };

            if (dialog.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("# Kable Hardware Packet Trace History Log");
                sb.AppendLine($"# Exported at: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine("# Seq | Time | Direction | Device | Kind | Length | Latency | Payload");
                sb.AppendLine(new string('-', 90));

                foreach (var pkt in Packets)
                {
                    sb.AppendLine($"[{pkt.SequenceNo,5}] {pkt.Timestamp} | {pkt.Direction} | {pkt.DeviceId,-12} | {pkt.Kind,-18} | {pkt.Length,4}B | {pkt.LatencyMs,7} | {pkt.AsciiPreview}");
                }

                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                StatusMessage = $"Saved {Packets.Count} packets to {Path.GetFileName(dialog.FileName)}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export Failed: {ex.Message}";
        }
    }
}
