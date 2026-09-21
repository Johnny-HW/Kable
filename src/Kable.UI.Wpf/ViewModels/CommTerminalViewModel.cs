namespace Kable.UI.Wpf.ViewModels;

using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Kable.Core;
using Kable.Observability;

/// <summary>
/// 수시 명령(CommandConsole), 상시 텔레메트리(TelemetryStream), 알람(AlarmList)을 조립한 복합 터미널 뷰모델입니다.
/// 각 컴포넌트 뷰모델을 외부에 공개하여 개별 바인딩할 수도 있고, 복합 뷰에서 통합 바인딩할 수도 있습니다.
/// </summary>
public partial class CommTerminalViewModel : ObservableObject, ICommObserver
{
    public CommandConsoleViewModel CommandConsole { get; }
    public TelemetryStreamViewModel TelemetryStream { get; }
    public AlarmListViewModel AlarmList { get; }

    public ChannelReader<PacketTraceRecord> CommandStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> PeriodicStream => throw new NotSupportedException();
    public ChannelReader<PacketTraceRecord> AlarmStream => throw new NotSupportedException();

    public event Func<string, Task>? ManualSendRequested
    {
        add => CommandConsole.ManualSendRequested += value;
        remove => CommandConsole.ManualSendRequested -= value;
    }

    public CommTerminalViewModel(Dispatcher? dispatcher = null, int maxLogCount = 1000)
    {
        CommandConsole = new CommandConsoleViewModel(dispatcher, maxLogCount);
        TelemetryStream = new TelemetryStreamViewModel(dispatcher);
        AlarmList = new AlarmListViewModel(dispatcher);
    }

    public void OnPacketTrace(in PacketTraceRecord trace)
    {
        switch (trace.Kind)
        {
            case TrafficKind.AperiodicCommand:
                CommandConsole.OnPacketTrace(in trace);
                break;
            case TrafficKind.PeriodicTelemetry:
                TelemetryStream.OnPacketTrace(in trace);
                break;
            case TrafficKind.SpontaneousAlarm:
                AlarmList.OnPacketTrace(in trace);
                break;
        }
    }
}
