namespace Kable.ConfigStudio.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kable.ConfigStudio.Services;
using Kable.Core;
using Kable.Localization;
using Kable.Observability;
using Kable.UI.Wpf.Models;
using Kable.UI.Wpf.Services;
using Kable.UI.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IConfigStudioFileService _fileService;
    private VirtualDeviceSimulator? _simulator;

    public IReadOnlyList<TransportType> AvailableTransports { get; } = Enum.GetValues<TransportType>();
    public IReadOnlyList<CodecType> AvailableCodecs { get; } = Enum.GetValues<CodecType>();
    public IReadOnlyList<RouterType> AvailableRouters { get; } = Enum.GetValues<RouterType>();
    public IReadOnlyList<int> AvailableBaudRates { get; } = [9600, 19200, 38400, 57600, 115200];
    public IReadOnlyList<string> AvailableParities { get; } = ["None", "Odd", "Even"];
    public IReadOnlyList<string> AvailableStopBits { get; } = ["One", "Two"];
    public IReadOnlyList<LanguageOption> AvailableLanguages { get; } = LanguageOption.DefaultLanguages;

    [ObservableProperty]
    private LanguageOption _selectedLanguage;

    [ObservableProperty]
    private int _selectedNavIndex; // 0: Config, 1: Packets, 2: LiveInspector, 3: Telemetry, 4: Alarms, 5: TomlExport

    public bool IsNavConfig => SelectedNavIndex == 0;
    public bool IsNavPackets => SelectedNavIndex == 1;
    public bool IsNavInspector => SelectedNavIndex == 2;
    public bool IsNavTelemetry => SelectedNavIndex == 3;
    public bool IsNavAlarms => SelectedNavIndex == 4;
    public bool IsNavExport => SelectedNavIndex == 5;

    [ObservableProperty]
    private ObservableCollection<PacketCatalogItem> _packetCatalog = new()
    {
        new PacketCatalogItem { Id = "REQ_STATUS", Name = "Device Status Query", Kind = TrafficKind.AperiodicCommand, CommandPayload = "GET_STATUS", ResponseTemplate = "STATUS: READY", IntervalMs = 0, Unit = "", SimulatedBaseValue = 0 },
        new PacketCatalogItem { Id = "CHAMBER_TEMP", Name = "Chamber Temperature", Kind = TrafficKind.PeriodicTelemetry, CommandPayload = "", ResponseTemplate = "TEMP:{VAL}", IntervalMs = 800, Unit = "°C", SimulatedBaseValue = 24.8 },
        new PacketCatalogItem { Id = "LINE_PRESSURE", Name = "Line Supply Pressure", Kind = TrafficKind.PeriodicTelemetry, CommandPayload = "", ResponseTemplate = "PRESS:{VAL}", IntervalMs = 1000, Unit = "kPa", SimulatedBaseValue = 101.3 },
        new PacketCatalogItem { Id = "FLOW_RATE", Name = "Chemical Flow Rate", Kind = TrafficKind.PeriodicTelemetry, CommandPayload = "", ResponseTemplate = "FLOW:{VAL}", IntervalMs = 600, Unit = "mL/min", SimulatedBaseValue = 12.5 },
        new PacketCatalogItem { Id = "ALM_OVERTEMP", Name = "Overtemp Detected Alarm", Kind = TrafficKind.SpontaneousAlarm, CommandPayload = "", ResponseTemplate = "ALM_001: OVERTEMP", IntervalMs = 0, Unit = "", SimulatedBaseValue = 0 }
    };

    [ObservableProperty]
    private bool _isSimulatorRunning;

    [ObservableProperty]
    private string _simulationSpeedLabel = "1.0x";

    public CommTerminalViewModel Terminal { get; } = new();

    [ObservableProperty]
    private ObservableCollection<string> _detectedComPorts = new();

    [ObservableProperty]
    private KableProfileModel _profile = new();

    [ObservableProperty]
    private string _testStatus = "Idle (No communication test run)";

    [ObservableProperty]
    private string _testResultColor = "#A6ADC8";

    [ObservableProperty]
    private string _liveChamberTemp = "24.8 °C";

    [ObservableProperty]
    private string _liveLinePressure = "101.3 kPa";

    [ObservableProperty]
    private string _liveFlowRate = "12.5 mL/min";

    [ObservableProperty]
    private string _liveVacuumSealPa = "12.4 Pa";

    [ObservableProperty]
    private double _liveVacuumProgress = 75.0;

    [ObservableProperty]
    private string _liveChamberThermalStatus = "Normal • 45°C";

    [ObservableProperty]
    private double _liveChamberThermalProgress = 38.0;

    [ObservableProperty]
    private string _activeFaultsSummary = "2 Warnings, 0 Crit";

    [ObservableProperty]
    private string _eStopChainStatus = "CLOSED (0.4ms)";

    [ObservableProperty]
    private string _mtbfHours = "418.4 hrs (100%)";

    [ObservableProperty]
    private string _auditSignature = "#8f2a9c (14:28 UTC)";

    [ObservableProperty]
    private string _supervisorPinStatus = "● ● ○ ○ (2 of 4 Digits)";

    [ObservableProperty]
    private bool _isMasterOverrideActive;

    [ObservableProperty]
    private string _generatedTomlPreview = string.Empty;

    [ObservableProperty]
    private string _generatedCSharpPreview = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _traceLog = new();

    public bool IsSerial => Profile.Transport == TransportType.Serial;
    public bool IsTcp => Profile.Transport == TransportType.Tcp;
    public bool IsNamedPipe => Profile.Transport == TransportType.NamedPipe;
    public bool IsModbusTcp => Profile.Transport == TransportType.ModbusTcp;
    public bool IsMelsecSlmp => Profile.Transport == TransportType.MelsecSlmp;
    public bool IsMqtt => Profile.Transport == TransportType.Mqtt;
    public bool IsOpcUa => Profile.Transport == TransportType.OpcUa;

    public MainViewModel()
        : this(ConfigStudioFileService.Instance)
    {
    }

    public MainViewModel(IConfigStudioFileService fileService)
    {
        ArgumentNullException.ThrowIfNull(fileService);
        _fileService = fileService;
        _selectedLanguage = AvailableLanguages[0];
        KableLocalizer.Instance.SetCulture(CultureInfo.GetCultureInfo(_selectedLanguage.CultureCode));

        InitializeTerminalAndCatalog();
    }

    partial void OnSelectedNavIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsNavConfig));
        OnPropertyChanged(nameof(IsNavPackets));
        OnPropertyChanged(nameof(IsNavInspector));
        OnPropertyChanged(nameof(IsNavTelemetry));
        OnPropertyChanged(nameof(IsNavAlarms));
        OnPropertyChanged(nameof(IsNavExport));
    }

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        if (value != null)
        {
            KableLocalizer.Instance.SetCulture(CultureInfo.GetCultureInfo(value.CultureCode));
        }
    }

    [RelayCommand]
    public void SetNav(string indexStr)
    {
        if (int.TryParse(indexStr, out int idx))
        {
            SelectedNavIndex = idx;
        }
    }

    [RelayCommand]
    public void CycleSimulationSpeed()
    {
        if (_simulator == null) return;
        if (Math.Abs(_simulator.SpeedMultiplier - 1.0) < 0.05)
        {
            _simulator.SpeedMultiplier = 2.0;
            SimulationSpeedLabel = "2.0x (Fast)";
        }
        else if (Math.Abs(_simulator.SpeedMultiplier - 2.0) < 0.05)
        {
            _simulator.SpeedMultiplier = 0.5;
            SimulationSpeedLabel = "0.5x (Slow)";
        }
        else
        {
            _simulator.SpeedMultiplier = 1.0;
            SimulationSpeedLabel = "1.0x (Normal)";
        }
    }

    [RelayCommand]
    public void ToggleSimulation()
    {
        _simulator?.Toggle();
        IsSimulatorRunning = _simulator?.IsRunning ?? false;
        if (IsSimulatorRunning)
        {
            TestStatus = "🟢 [Simulator Active] Streaming with realistic hardware latency...";
            TestResultColor = "#A6E3A1";
        }
        else
        {
            TestStatus = "⏹️ [Simulator Stopped]";
            TestResultColor = "#A6ADC8";
        }
    }

    [RelayCommand]
    public void AddPacketItem()
    {
        int count = PacketCatalog.Count + 1;
        var newItem = new PacketCatalogItem
        {
            Id = $"CUSTOM_PKT_{count}",
            Name = $"User Packet {count}",
            Kind = TrafficKind.PeriodicTelemetry,
            CommandPayload = "",
            ResponseTemplate = "{VAL}",
            IntervalMs = 200,
            Unit = "V",
            SimulatedBaseValue = 5.0
        };
        AttachCatalogItemSync(newItem);
        PacketCatalog.Add(newItem);
        SyncCommandConsole();
    }

    public void SyncCommandConsole()
    {
        var scheduledItems = PacketCatalog.Select(p => p.ToScheduledItem());
        Terminal.CommandConsole.LoadScheduledCommands(scheduledItems);
    }

    [RelayCommand]
    public void TripEmergencyStop()
    {
        EStopChainStatus = "TRIPPED (HARD STOP)";
        ActiveFaultsSummary = "1 CRITICAL, 2 Warnings";
        TestStatus = "🚨 [HARD STOP] Emergency E-Stop Tripped! Safety Loop Open.";
        TestResultColor = "#F38BA8";

        Terminal.OnPacketTrace(new PacketTraceRecord(
            DateTime.UtcNow,
            PacketDirection.Rx,
            TrafficKind.SpontaneousAlarm,
            "ESTOP_TRIP",
            Encoding.UTF8.GetBytes("CRIT_001: EMERGENCY HARD STOP TRIPPED"),
            "EMERGENCY HARD STOP ACTIVATED BY OPERATOR",
            TimeSpan.Zero,
            LogLevel.Critical,
            Profile.DeviceName));
    }

    [RelayCommand]
    public void ResetInterlocks()
    {
        EStopChainStatus = "CLOSED (0.4ms)";
        ActiveFaultsSummary = "0 Warning, 0 Crit";
        TestStatus = "🟢 [Safety Interlocks Reset] All safety loops verified nominal.";
        TestResultColor = "#A6E3A1";
        Terminal.AlarmList.ClearAlarms();
    }

    [RelayCommand]
    public void AuthenticateSupervisor()
    {
        SupervisorPinStatus = "● ● ● ● (AUTHENTICATED)";
        IsMasterOverrideActive = !IsMasterOverrideActive;
        TestStatus = IsMasterOverrideActive ? "⚠️ [Supervisor Key] Master Override Active" : "🔒 [Supervisor Key] Normal Security Lock";
    }
}
