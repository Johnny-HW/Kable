namespace Kable.UI.Wpf.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kable.Core;
using Kable.Observability;

public partial class CommandConsoleViewModel : ObservableObject, ICommObserver
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
    private string _manualCommandText = string.Empty;

    public ObservableCollection<PacketDisplayModel> Packets { get; } = new();

    public ChannelReader<PacketTraceRecord> CommandStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> PeriodicStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> AlarmStream => throw new NotSupportedException();

    public event Func<string, Task>? ManualSendRequested;

    public CommandConsoleViewModel(Dispatcher? dispatcher = null, int maxLogCount = 1000)
    {
        _dispatcher = dispatcher ?? (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
        _maxLogCount = maxLogCount;
    }

    public void OnPacketTrace(in PacketTraceRecord trace)
    {
        // 수시 통신 (명령/응답) 필터링
        if (trace.Kind != TrafficKind.AperiodicCommand) return;
        if (IsPaused) return;

        var raw = trace.RawBytes.ToArray();
        var model = new PacketDisplayModel
        {
            SequenceNo = trace.SequenceNo,
            Timestamp = trace.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff"),
            Direction = trace.Direction == PacketDirection.Tx ? "TX ➡" : "RX ⬅",
            DeviceId = trace.DeviceId,
            Kind = trace.Tag.Length > 0 ? trace.Tag : trace.Kind.ToString(),
            Length = raw.Length,
            AsciiPreview = trace.ParsedText ?? Encoding.ASCII.GetString(raw).Replace("\r", "\\r").Replace("\n", "\\n"),
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
    }

    [RelayCommand]
    public void TogglePause()
    {
        IsPaused = !IsPaused;
    }

    [RelayCommand]
    public async Task SendManualCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualCommandText)) return;

        if (ManualSendRequested != null)
        {
            await ManualSendRequested.Invoke(ManualCommandText);
        }
    }
}
