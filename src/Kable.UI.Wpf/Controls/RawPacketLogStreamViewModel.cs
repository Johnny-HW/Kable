namespace Kable.UI.Wpf.Controls;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kable.Core;
using Kable.Observability;
using Kable.UI.Wpf.Services;
using Kable.UI.Wpf.ViewModels;

public partial class RawPacketLogStreamViewModel : ObservableObject, ICommObserver, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly int _maxLogCount;
    private readonly int _maxPendingCount;
    private readonly IPacketLogExportService _logExportService;
    private readonly ConcurrentQueue<PacketDisplayModel> _pendingPackets = new();
    private readonly DispatcherTimer _flushTimer;
    private int _pendingCount;
    private int _isPausedSnapshot;
    private string _filterSnapshot = string.Empty;
    private int _disposed;

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
        : this(dispatcher, maxLogCount, WpfPacketLogExportService.Instance)
    {
    }

    public RawPacketLogStreamViewModel(Dispatcher? dispatcher, int maxLogCount, IPacketLogExportService? logExportService)
    {
        _dispatcher = dispatcher ?? (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
        _maxLogCount = Math.Max(1, maxLogCount);
        _maxPendingCount = Math.Max(_maxLogCount * 2, 2048);
        _logExportService = logExportService ?? WpfPacketLogExportService.Instance;
        _flushTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Background, (_, _) => FlushPending(), _dispatcher);
        _flushTimer.Start();
    }

    partial void OnIsPausedChanged(bool value) => Volatile.Write(ref _isPausedSnapshot, value ? 1 : 0);

    partial void OnFilterTextChanged(string value) => Volatile.Write(ref _filterSnapshot, value ?? string.Empty);

    public void OnPacketTrace(in PacketTraceRecord trace)
    {
        if (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _isPausedSnapshot) != 0) return;

        var raw = trace.RawBytes.ToArray();
        string ascii = trace.ParsedText ?? Encoding.ASCII.GetString(raw).Replace("\r", "\\r").Replace("\n", "\\n");
        string filter = Volatile.Read(ref _filterSnapshot);

        if (filter.Length > 0 &&
            !ascii.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
            !trace.DeviceId.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
            !trace.Tag.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return;
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

        _pendingPackets.Enqueue(model);
        if (Interlocked.Increment(ref _pendingCount) > _maxPendingCount && _pendingPackets.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _pendingCount);
        }
    }

    /// <summary>대기 중인 원본 패킷 배치를 UI 컬렉션에 일괄 반영합니다.</summary>
    public void FlushPending()
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(FlushPending);
            return;
        }

        if (_pendingPackets.IsEmpty) return;

        var batch = new List<PacketDisplayModel>();
        while (_pendingPackets.TryDequeue(out var item))
        {
            Interlocked.Decrement(ref _pendingCount);
            batch.Add(item);
        }

        if (batch.Count == 0) return;

        if (batch.Count >= _maxLogCount)
        {
            Packets.Clear();
            for (int i = batch.Count - 1; i >= batch.Count - _maxLogCount; i--)
            {
                Packets.Add(batch[i]);
            }
            return;
        }

        int overflow = Packets.Count + batch.Count - _maxLogCount;
        for (int i = 0; i < overflow && Packets.Count > 0; i++)
        {
            Packets.RemoveAt(Packets.Count - 1);
        }

        for (int i = 0; i < batch.Count; i++)
        {
            Packets.Insert(0, batch[i]);
        }
    }

    public void UpdateHealthMetrics(CommHealthMetrics metrics)
    {
        void Apply()
        {
            ConnectionState = metrics.State;
            RttText = metrics.LastRttMs > 0 ? $"{metrics.LastRttMs:F1} ms (avg {metrics.AvgRttMs:F1})" : "- ms";
            PacketCounterText = $"TX: {metrics.TotalTxPackets:N0} / RX: {metrics.TotalRxPackets:N0}";
            ErrorRateText = $"Err: {metrics.ErrorRatePercentage:F2}% ({metrics.ErrorPackets:N0})";

            StateBadgeColor = metrics.State switch
            {
                DeviceConnectionState.Connected => "#34C759",
                DeviceConnectionState.Simulated => "#FF9F0A",
                DeviceConnectionState.Connecting => "#0071E3",
                DeviceConnectionState.Faulted => "#FF3B30",
                _ => "#94A3B8"
            };
        }

        if (_dispatcher.CheckAccess()) Apply();
        else _dispatcher.BeginInvoke(Apply);
    }

    [RelayCommand]
    public void ClearLogs()
    {
        while (_pendingPackets.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _pendingCount);
        }

        void ClearUi()
        {
            Packets.Clear();
            SelectedPacket = null;
            StatusMessage = "Logs cleared";
        }

        if (_dispatcher.CheckAccess()) ClearUi();
        else _dispatcher.BeginInvoke(ClearUi);
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
        FlushPending();
        try
        {
            string? savedPath = _logExportService.ExportPacketsWithDialog(
                Packets,
                "Export Packet Stream Logs",
                "Kable_PacketStream",
                "Kable Hardware Packet Trace History Log");

            if (!string.IsNullOrEmpty(savedPath))
            {
                StatusMessage = $"Saved {Packets.Count} packets to {Path.GetFileName(savedPath)}";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException)
        {
            Debug.WriteLine($"[RawPacketLogStreamViewModel] ExportToFile failed: {ex.Message}");
            StatusMessage = $"Export Failed: {ex.Message}";
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        if (_dispatcher.CheckAccess())
        {
            _flushTimer.Stop();
        }
        else if (!_dispatcher.HasShutdownStarted)
        {
            _dispatcher.BeginInvoke(() => _flushTimer.Stop());
        }
    }
}
