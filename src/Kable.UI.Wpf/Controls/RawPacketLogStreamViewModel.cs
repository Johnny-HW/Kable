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

    [ObservableProperty]
    private DeviceConnectionState _connectionState = DeviceConnectionState.Disconnected;

    [ObservableProperty]
    private string _rttText = "- ms";

    [ObservableProperty]
    private string _packetCounterText = "TX: 0 / RX: 0";

    [ObservableProperty]
    private string _errorRateText = "Err: 0.0%";

    [ObservableProperty]
    private string _stateBadgeColor = "#94A3B8";

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
                Packets.RemoveAt(Packets.Count - 1);
            }
            Packets.Insert(0, model);
        });
    }

    /// <summary>
    /// 외부 세션 또는 백그라운드 폴러로부터 통신 건전성 지표를 전달받아 UI를 갱신합니다.
    /// </summary>
    public void UpdateHealthMetrics(CommHealthMetrics metrics)
    {
        _dispatcher.BeginInvoke(() =>
        {
            ConnectionState = metrics.State;
            RttText = metrics.LastRttMs > 0 ? $"{metrics.LastRttMs:F1} ms (avg {metrics.AvgRttMs:F1})" : "- ms";
            PacketCounterText = $"TX: {metrics.TotalTxPackets:N0} / RX: {metrics.TotalRxPackets:N0}";
            ErrorRateText = $"Err: {metrics.ErrorRatePercentage:F2}% ({metrics.ErrorPackets:N0})";

            StateBadgeColor = metrics.State switch
            {
                DeviceConnectionState.Connected => "#34C759", // Green
                DeviceConnectionState.Simulated => "#FF9F0A", // Orange/Yellow
                DeviceConnectionState.Connecting => "#0071E3", // Blue
                DeviceConnectionState.Faulted => "#FF3B30",   // Red
                _ => "#94A3B8"                               // Gray
            };
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
