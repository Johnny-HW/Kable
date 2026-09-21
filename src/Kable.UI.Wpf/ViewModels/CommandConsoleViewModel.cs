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
    private string _commandSearchText = string.Empty;

    [ObservableProperty]
    private SettingParameterModel? _selectedSetting;

    public ObservableCollection<PacketDisplayModel> Packets { get; } = new();

    public ObservableCollection<SettingParameterModel> AllSettingParameters { get; } = new();

    public ObservableCollection<SettingParameterModel> FilteredSettingParameters { get; } = new();

    public ChannelReader<PacketTraceRecord> CommandStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> PeriodicStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> AlarmStream => throw new NotSupportedException();

    public event Func<string, Task>? ManualSendRequested;

    public CommandConsoleViewModel(Dispatcher? dispatcher = null, int maxLogCount = 1000)
    {
        _dispatcher = dispatcher ?? (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
        _maxLogCount = maxLogCount;
        RefreshFilteredCommands();
        _selectedSetting = FilteredSettingParameters.Count > 0 ? FilteredSettingParameters[0] : null;
    }

    /// <summary>
    /// 외부에서 순수 명령어 정의 구조체(CommandDefinition) 목록을 주입하여 수시 명령 콘솔 파라미터 셋을 동적 구성
    /// </summary>
    public void LoadCommands(IEnumerable<Kable.Protocol.CommandDefinition> commandDefs)
    {
        AllSettingParameters.Clear();
        if (commandDefs != null)
        {
            foreach (var def in commandDefs)
            {
                AllSettingParameters.Add(SettingParameterModel.FromDefinition(def));
            }
        }
        RefreshFilteredCommands();
        SelectedSetting = FilteredSettingParameters.Count > 0 ? FilteredSettingParameters[0] : null;
    }

    /// <summary>
    /// 외부에서 상시/수시 스케줄링 목록(ScheduledCommandItem)을 주입받아 수시(Aperiodic) 모드인 항목만 자동으로 필터링하여 구성
    /// </summary>
    public void LoadScheduledCommands(IEnumerable<Kable.Protocol.ScheduledCommandItem> scheduledItems)
    {
        AllSettingParameters.Clear();
        if (scheduledItems != null)
        {
            foreach (var item in scheduledItems)
            {
                if (item.IsEnabled && item.Mode == Kable.Protocol.CommandExecutionMode.Aperiodic)
                {
                    AllSettingParameters.Add(SettingParameterModel.FromDefinition(item.Definition));
                }
            }
        }
        RefreshFilteredCommands();
        SelectedSetting = FilteredSettingParameters.Count > 0 ? FilteredSettingParameters[0] : null;
    }

    partial void OnCommandSearchTextChanged(string value)
    {
        RefreshFilteredCommands();
    }

    public void RefreshFilteredCommands()
    {
        FilteredSettingParameters.Clear();
        string query = CommandSearchText?.Trim() ?? string.Empty;

        foreach (var p in AllSettingParameters)
        {
            if (string.IsNullOrWhiteSpace(query) ||
                p.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.CommandPrefix.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredSettingParameters.Add(p);
            }
        }
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
                Packets.RemoveAt(Packets.Count - 1);
            }
            Packets.Insert(0, model);
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
    public void ExportToFile()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Aperiodic Command Logs",
                Filter = "Log Files (*.log)|*.log|CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = $"Kable_CommandLogs_{DateTime.Now:yyyyMMdd_HHmmss}.log"
            };

            if (dialog.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("# Kable Aperiodic Command & Setting Trace Log");
                sb.AppendLine($"# Exported at: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine("# Seq | Time | Direction | Device | Kind | Length | Latency | Payload");
                sb.AppendLine(new string('-', 90));

                foreach (var pkt in Packets)
                {
                    sb.AppendLine($"[{pkt.SequenceNo,5}] {pkt.Timestamp} | {pkt.Direction} | {pkt.DeviceId,-12} | {pkt.Kind,-18} | {pkt.Length,4}B | {pkt.LatencyMs,7} | {pkt.AsciiPreview}");
                }

                System.IO.File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // ignore or log
        }
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
        target.LastAckMessage = $"Sending: {payload}";

        if (ManualSendRequested != null)
        {
            await ManualSendRequested.Invoke(payload);
            target.LastAckMessage = $"Applied ({DateTime.Now:HH:mm:ss})";
        }
    }
}
