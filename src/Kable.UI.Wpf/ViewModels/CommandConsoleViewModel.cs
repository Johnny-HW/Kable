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
using Kable.UI.Wpf.Models;

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

    [ObservableProperty]
    private SettingParameterModel? _selectedSetting;

    public ObservableCollection<PacketDisplayModel> Packets { get; } = new();

    public ObservableCollection<SettingParameterModel> SettingParameters { get; } = new()
    {
        new SettingParameterModel
        {
            Id = "SET_TARGET_TEMP",
            DisplayName = "목표 온도 제어 (Chamber Temp)",
            CommandPrefix = "SET TEMP_TARGET",
            Value = 45.0,
            MinValue = 10.0,
            MaxValue = 120.0,
            Step = 0.5,
            Unit = "°C"
        },
        new SettingParameterModel
        {
            Id = "SET_PRESS_LIMIT",
            DisplayName = "공급 압력 상한치 (Pressure Limit)",
            CommandPrefix = "SET PRESS_LIMIT",
            Value = 150.0,
            MinValue = 50.0,
            MaxValue = 300.0,
            Step = 1.0,
            Unit = "kPa"
        },
        new SettingParameterModel
        {
            Id = "SET_FLOW_OFFSET",
            DisplayName = "약액 유량 오프셋 보정 (Flow Offset)",
            CommandPrefix = "SET FLOW_OFFSET",
            Value = 1.5,
            MinValue = -5.0,
            MaxValue = 10.0,
            Step = 0.1,
            Unit = "mL/min"
        }
    };

    public ChannelReader<PacketTraceRecord> CommandStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> PeriodicStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> AlarmStream => throw new NotSupportedException();

    public event Func<string, Task>? ManualSendRequested;

    public CommandConsoleViewModel(Dispatcher? dispatcher = null, int maxLogCount = 1000)
    {
        _dispatcher = dispatcher ?? (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
        _maxLogCount = maxLogCount;
        _selectedSetting = SettingParameters[0];
    }

    public void OnPacketTrace(in PacketTraceRecord trace)
    {
        // 수시 통신 (명령/응답)만 필터링 - 상시(Periodic) 및 알람(Alarm)은 제외
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

    [RelayCommand]
    public async Task ApplySettingAsync(SettingParameterModel? setting)
    {
        var target = setting ?? SelectedSetting;
        if (target == null) return;

        string payload = target.BuildPayload();
        target.LastAckMessage = $"전송 중: {payload}";

        if (ManualSendRequested != null)
        {
            await ManualSendRequested.Invoke(payload);
            target.LastAckMessage = $"적용 완료 ({DateTime.Now:HH:mm:ss})";
        }
    }
}
