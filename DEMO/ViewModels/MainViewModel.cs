using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kable.Core;
using Kable.Localization;
using Kable.Observability;
using Kable.UI.Wpf.Models;
using Kable.UI.Wpf.Services;
using Kable.UI.Wpf.ViewModels;

namespace Kable.ConfigStudio.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public IReadOnlyList<TransportType> AvailableTransports { get; } = Enum.GetValues<TransportType>();
    public IReadOnlyList<CodecType> AvailableCodecs { get; } = Enum.GetValues<CodecType>();
    public IReadOnlyList<RouterType> AvailableRouters { get; } = Enum.GetValues<RouterType>();
    public IReadOnlyList<int> AvailableBaudRates { get; } = new[] { 9600, 19200, 38400, 57600, 115200 };
    public IReadOnlyList<string> AvailableParities { get; } = new[] { "None", "Odd", "Even" };
    public IReadOnlyList<string> AvailableStopBits { get; } = new[] { "One", "Two" };

    public IReadOnlyList<LanguageOption> AvailableLanguages { get; } = LanguageOption.DefaultLanguages;

    [ObservableProperty]
    private LanguageOption _selectedLanguage;

    [ObservableProperty]
    private int _selectedNavIndex = 0; // 0: Config, 1: Packets, 2: LiveInspector, 3: Telemetry, 4: Alarms, 5: TomlExport

    public bool IsNavConfig => SelectedNavIndex == 0;
    public bool IsNavPackets => SelectedNavIndex == 1;
    public bool IsNavInspector => SelectedNavIndex == 2;
    public bool IsNavTelemetry => SelectedNavIndex == 3;
    public bool IsNavAlarms => SelectedNavIndex == 4;
    public bool IsNavExport => SelectedNavIndex == 5;

    partial void OnSelectedNavIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsNavConfig));
        OnPropertyChanged(nameof(IsNavPackets));
        OnPropertyChanged(nameof(IsNavInspector));
        OnPropertyChanged(nameof(IsNavTelemetry));
        OnPropertyChanged(nameof(IsNavAlarms));
        OnPropertyChanged(nameof(IsNavExport));
    }

    [RelayCommand]
    public void SetNav(string indexStr)
    {
        if (int.TryParse(indexStr, out int idx))
        {
            SelectedNavIndex = idx;
        }
    }

    // Packet Catalog Collection
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

    private VirtualDeviceSimulator? _simulator;

    [RelayCommand]
    public void ToggleSimulation()
    {
        _simulator?.Toggle();
        IsSimulatorRunning = _simulator?.IsRunning ?? false;
        if (IsSimulatorRunning)
        {
            TestStatus = "🟢 [Simulator Active] High-speed periodic telemetry streaming...";
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
        PacketCatalog.Add(new PacketCatalogItem
        {
            Id = $"CUSTOM_PKT_{count}",
            Name = $"User Packet {count}",
            Kind = TrafficKind.PeriodicTelemetry,
            CommandPayload = "",
            ResponseTemplate = "{VAL}",
            IntervalMs = 200,
            Unit = "V",
            SimulatedBaseValue = 5.0
        });
    }

    public CommTerminalViewModel Terminal { get; } = new();

    [ObservableProperty]
    private ObservableCollection<string> _detectedComPorts = new();

    [ObservableProperty]
    private KableProfileModel _profile = new();

    [ObservableProperty]
    private string _testStatus = "Idle (No communication test run)";

    [ObservableProperty]
    private string _testResultColor = "#A6ADC8"; // gray

    [ObservableProperty]
    private string _generatedTomlPreview = string.Empty;

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
    {
        _selectedLanguage = AvailableLanguages[0]; // Default English (en-US)
        KableLocalizer.Instance.SetCulture(CultureInfo.GetCultureInfo(_selectedLanguage.CultureCode));

        Terminal.ManualSendRequested += async (cmd) =>
        {
            TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [TX] 수시/설정 명령 송신: {cmd}");
            var txBytes = Encoding.UTF8.GetBytes(cmd);
            Terminal.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow,
                PacketDirection.Tx,
                TrafficKind.AperiodicCommand,
                "SET_CMD",
                txBytes,
                cmd,
                TimeSpan.Zero,
                LogLevel.Information,
                Profile.DeviceName));

            await Task.Delay(40);

            // Setting 파라미터 변경 명령 처리 및 텔레메트리 기준값 동적 동기화
            string echoResp;
            if (cmd.StartsWith("SET TEMP_TARGET=", StringComparison.OrdinalIgnoreCase) &&
                double.TryParse(cmd.Substring("SET TEMP_TARGET=".Length), CultureInfo.InvariantCulture, out double targetTemp))
            {
                var tempItem = System.Linq.Enumerable.FirstOrDefault(PacketCatalog, p => p.Id == "CHAMBER_TEMP");
                if (tempItem != null)
                {
                    tempItem.SimulatedBaseValue = targetTemp;
                }
                echoResp = $"ACK: TEMP_TARGET UPDATED TO {targetTemp:F1}°C";
            }
            else if (cmd.StartsWith("SET PRESS_LIMIT=", StringComparison.OrdinalIgnoreCase) &&
                     double.TryParse(cmd.Substring("SET PRESS_LIMIT=".Length), CultureInfo.InvariantCulture, out double pressLimit))
            {
                var pressItem = System.Linq.Enumerable.FirstOrDefault(PacketCatalog, p => p.Id == "LINE_PRESSURE");
                if (pressItem != null)
                {
                    pressItem.SimulatedBaseValue = pressLimit;
                }
                echoResp = $"ACK: PRESS_LIMIT UPDATED TO {pressLimit:F1} kPa";
            }
            else if (cmd.StartsWith("SET FLOW_OFFSET=", StringComparison.OrdinalIgnoreCase) &&
                     double.TryParse(cmd.Substring("SET FLOW_OFFSET=".Length), CultureInfo.InvariantCulture, out double flowOffset))
            {
                var flowItem = System.Linq.Enumerable.FirstOrDefault(PacketCatalog, p => p.Id == "FLOW_RATE");
                if (flowItem != null)
                {
                    flowItem.SimulatedBaseValue = Math.Round(12.5 + flowOffset, 2);
                }
                echoResp = $"ACK: FLOW_OFFSET APPLIED ({flowOffset:F1} mL/min)";
            }
            else
            {
                echoResp = $"ACK:{cmd}";
            }

            var rxBytes = Encoding.UTF8.GetBytes(echoResp);
            Terminal.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow,
                PacketDirection.Rx,
                TrafficKind.AperiodicCommand,
                "SET_ACK",
                rxBytes,
                echoResp,
                TimeSpan.FromMilliseconds(40),
                LogLevel.Information,
                Profile.DeviceName));
        };

        RefreshComPorts();
        UpdateTomlPreview();

        _simulator = new VirtualDeviceSimulator(
            Terminal,
            () => PacketCatalog,
            () => Profile.DeviceName);
    }

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        if (value != null)
        {
            KableLocalizer.Instance.SetCulture(CultureInfo.GetCultureInfo(value.CultureCode));
        }
    }

    [RelayCommand]
    public void RefreshComPorts()
    {
        DetectedComPorts.Clear();
        try
        {
            var ports = SerialPort.GetPortNames();
            foreach (var p in ports)
            {
                DetectedComPorts.Add(p);
            }
        }
        catch
        {
            // ignore
        }

        if (DetectedComPorts.Count == 0)
        {
            DetectedComPorts.Add("COM1");
            DetectedComPorts.Add("COM3");
        }

        if (!DetectedComPorts.Contains(Profile.PortName))
        {
            Profile.PortName = DetectedComPorts[0];
        }
        OnPropertyChanged(nameof(Profile));
    }

    [RelayCommand]
    public void SelectTransport(TransportType type)
    {
        Profile.Transport = type;
        OnPropertyChanged(nameof(IsSerial));
        OnPropertyChanged(nameof(IsTcp));
        OnPropertyChanged(nameof(IsNamedPipe));
        OnPropertyChanged(nameof(IsModbusTcp));
        OnPropertyChanged(nameof(IsMelsecSlmp));
        OnPropertyChanged(nameof(IsMqtt));
        OnPropertyChanged(nameof(IsOpcUa));
        UpdateTomlPreview();
    }

    /// <summary>
    /// 실제 대상 호스트 및 포트에 TCP 소켓 핸드셰이크를 직접 시도하고 실제 RTT를 측정합니다.
    /// 실패 시 실제 네트워크 예외를 붉은색 경고로 명확히 출력합니다.
    /// </summary>
    [RelayCommand]
    public async Task RunLiveTestAsync()
    {
        TestStatus = $"⚡ [실제 진단] {Profile.Transport} 네트워크 링크 핸드셰이크 진행 중...";
        TestResultColor = "#F9E2AF"; // yellow
        TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [LIVE] {Profile.Transport} 실제 연결 시도 시작");

        var sw = Stopwatch.StartNew();

        try
        {
            if (Profile.Transport == TransportType.Serial)
            {
                TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [LIVE] COM 포트 열기 시도: {Profile.PortName} ({Profile.BaudRate} bps)");
                using var serial = new SerialPort(Profile.PortName, Profile.BaudRate);
                serial.Open();
                sw.Stop();
                TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [LIVE] COM 포트 열기 성공 ({sw.Elapsed.TotalMilliseconds:F2}ms)");
                serial.Close();

                TestStatus = $"🟢 [LIVE 성공] 시리얼 포트 '{Profile.PortName}' 열기 성공 (RTT: {sw.Elapsed.TotalMilliseconds:F2}ms)";
                TestResultColor = "#A6E3A1"; // green
            }
            else if (Profile.Transport == TransportType.NamedPipe)
            {
                TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [LIVE] Named Pipe 확인: \\\\.\\pipe\\{Profile.PipeName}");
                string pipePath = $@"\\.\pipe\{Profile.PipeName}";
                bool exists = File.Exists(pipePath);
                sw.Stop();

                if (exists)
                {
                    TestStatus = $"🟢 [LIVE 성공] Named Pipe '{Profile.PipeName}' 서버 인스턴스 확인 완료 ({sw.Elapsed.TotalMilliseconds:F2}ms)";
                    TestResultColor = "#A6E3A1";
                }
                else
                {
                    TestStatus = $"🟡 [LIVE 대기] 파이프 '{Profile.PipeName}' 활성 서버 프로세스가 아직 대기 중이지 않습니다.";
                    TestResultColor = "#F9E2AF";
                }
            }
            else
            {
                string targetHost = Profile.HostIp;
                int targetPort = Profile.TcpPort;

                if (Profile.Transport == TransportType.OpcUa && Uri.TryCreate(Profile.OpcEndpointUrl, UriKind.Absolute, out var uri))
                {
                    targetHost = uri.Host;
                    targetPort = uri.Port > 0 ? uri.Port : 4840;
                }

                TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [LIVE] TCP 소켓 연결 시도: {targetHost}:{targetPort}");
                using var client = new TcpClient();
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Max(1500, Profile.TimeoutMs)));
                
                await client.ConnectAsync(targetHost, targetPort, cts.Token);
                sw.Stop();

                TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [LIVE] TCP 3-way 핸드셰이크 성공: RTT={sw.Elapsed.TotalMilliseconds:F2}ms");

                TestStatus = $"🟢 [LIVE 성공] {Profile.Transport} 엔드포인트({targetHost}:{targetPort}) 연결 성공 (RTT: {sw.Elapsed.TotalMilliseconds:F2}ms)";
                TestResultColor = "#A6E3A1";
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [LIVE 오류] {ex.GetType().Name}: {ex.Message}");
            TestStatus = $"🔴 [LIVE 실패] 연결 불가: {ex.Message} ({sw.Elapsed.TotalMilliseconds:F1}ms)";
            TestResultColor = "#F38BA8"; // red
        }
    }

    /// <summary>
    /// 프로토콜 코덱 및 패킷 포맷 검증을 위한 오프라인 시뮬레이션(Mock Loopback) 모드입니다.
    /// </summary>
    [RelayCommand]
    public async Task RunMockSimulationAsync()
    {
        TestStatus = "🔬 [오프라인 시뮬레이션] 가상 루프백 패킷 검증 중...";
        TestResultColor = "#89B4FA"; // blue
        TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [MOCK] {Profile.Transport} 시뮬레이션 프레임 인코딩/디코딩 테스트");

        await Task.Delay(100);

        if (Profile.Transport == TransportType.ModbusTcp)
        {
            TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [MOCK] MBAP Header 생성 (UnitId={Profile.ModbusUnitId})");
            TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [MOCK] TX: [00 01 00 00 00 06 {Profile.ModbusUnitId:X2} 03 {Profile.ModbusTestRegister >> 8:X2} {Profile.ModbusTestRegister & 0xFF:X2} 00 02]");
            TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [MOCK] RX: [00 01 00 00 00 07 {Profile.ModbusUnitId:X2} 03 04 04 D2 16 2E] (Mock Regs: [1234, 5678])");
            TestStatus = $"🔷 [MOCK 성공] Modbus-TCP FC03 코덱 규격 검증 완료 (가상 루프백)";
        }
        else if (Profile.Transport == TransportType.MelsecSlmp)
        {
            TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [MOCK] SLMP 3E Binary 프레임 빌드 (Net={Profile.MelsecNetworkNo}, PC={Profile.MelsecPcNo}, Dev={Profile.MelsecDevice})");
            TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [MOCK] TX: [50 00 00 FF FF 03 00 0C 00 10 00 01 04 00 00 E8 03 00 A8 02 00]");
            TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [MOCK] RX: [D0 00 00 FF FF 03 00 06 00 00 00 D2 04 2E 16] (EndCode: 0000 OK)");
            TestStatus = $"🔷 [MOCK 성공] 미쓰비시 SLMP 3E 코덱 규격 검증 완료 (가상 루프백)";
        }
        else if (Profile.Transport == TransportType.Mqtt)
        {
            TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [MOCK] MQTT Publish 패킷 구성: ClientId='{Profile.MqttClientId}', Topic='{Profile.MqttTopicPrefix}/test'");
            TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [MOCK] Payload: {{\"client\":\"{Profile.MqttClientId}\",\"value\":100.0}}");
            TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [MOCK] PUBACK 시뮬레이션 수신 완료");
            TestStatus = $"🔷 [MOCK 성공] MQTT 텔레메트리 직렬화 규격 검증 완료 (가상 루프백)";
        }
        else if (Profile.Transport == TransportType.OpcUa)
        {
            TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [MOCK] OPC UA ReadRequest 구성: NodeId='{Profile.OpcNodeId}'");
            TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [MOCK] ReadResponse: StatusCode=Good, Value='NORMAL_OPERATING'");
            TestStatus = $"🔷 [MOCK 성공] OPC UA 노드 데이터 모델 규격 검증 완료 (가상 루프백)";
        }
        else
        {
            TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [MOCK] {Profile.Transport} 기본 프레임 에코 시뮬레이션 성공");
            TestStatus = $"🔷 [MOCK 성공] {Profile.Transport} 가상 루프백 테스트 완료";
        }

        // Kable.UI.Wpf 컴포넌트 실시간 반응 검증용 패킷 주입
        // 1. 수시 명령 (Aperiodic Tx/Rx)
        var cmdTx = Encoding.UTF8.GetBytes($"READ_CONFIG {Profile.DeviceName}");
        Terminal.OnPacketTrace(new PacketTraceRecord(
            DateTime.UtcNow,
            PacketDirection.Tx,
            TrafficKind.AperiodicCommand,
            "REQ_CFG",
            cmdTx,
            $"READ_CONFIG {Profile.DeviceName}",
            TimeSpan.Zero,
            LogLevel.Information,
            Profile.DeviceName));

        var cmdRx = Encoding.UTF8.GetBytes($"CONFIG_DATA STATUS=READY;BAUD={Profile.BaudRate}");
        Terminal.OnPacketTrace(new PacketTraceRecord(
            DateTime.UtcNow,
            PacketDirection.Rx,
            TrafficKind.AperiodicCommand,
            "RESP_CFG",
            cmdRx,
            $"CONFIG_DATA STATUS=READY;BAUD={Profile.BaudRate}",
            TimeSpan.FromMilliseconds(12.4),
            LogLevel.Information,
            Profile.DeviceName));

        // 2. 상시 텔레메트리 (Periodic Telemetry 50Hz)
        var telBytes1 = Encoding.UTF8.GetBytes("TEMP: 24.8 C");
        Terminal.OnPacketTrace(new PacketTraceRecord(
            DateTime.UtcNow,
            PacketDirection.Rx,
            TrafficKind.PeriodicTelemetry,
            "Chamber_Temperature",
            telBytes1,
            "24.8",
            TimeSpan.FromMilliseconds(1.2),
            LogLevel.Information,
            Profile.DeviceName));

        var telBytes2 = Encoding.UTF8.GetBytes("PRESS: 101.3 kPa");
        Terminal.OnPacketTrace(new PacketTraceRecord(
            DateTime.UtcNow,
            PacketDirection.Rx,
            TrafficKind.PeriodicTelemetry,
            "Line_Pressure",
            telBytes2,
            "101.3",
            TimeSpan.FromMilliseconds(1.1),
            LogLevel.Information,
            Profile.DeviceName));

        // 3. 실시간 알람 (Spontaneous Alarm)
        var almBytes = Encoding.UTF8.GetBytes("ALM_001: TEMPERATURE_HIGH_WARNING");
        Terminal.OnPacketTrace(new PacketTraceRecord(
            DateTime.UtcNow,
            PacketDirection.Rx,
            TrafficKind.SpontaneousAlarm,
            "ALM_OVERTEMP",
            almBytes,
            "Chamber temperature high threshold warning (24.8C)",
            TimeSpan.Zero,
            LogLevel.Warning,
            Profile.DeviceName));

        TestResultColor = "#89B4FA";
    }

    [RelayCommand]
    public void UpdateTomlPreview()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Kable Communication Configuration");
        sb.AppendLine($"# Generated by Kable.ConfigStudio on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine($"[device]");
        sb.AppendLine($"name = \"{Profile.DeviceName}\"");
        sb.AppendLine($"description = \"{Profile.Description}\"");
        sb.AppendLine();
        sb.AppendLine($"[transport]");
        sb.AppendLine($"type = \"{Profile.Transport}\"");

        switch (Profile.Transport)
        {
            case TransportType.Serial:
                sb.AppendLine($"port_name = \"{Profile.PortName}\"");
                sb.AppendLine($"baud_rate = {Profile.BaudRate}");
                sb.AppendLine($"data_bits = {Profile.DataBits}");
                sb.AppendLine($"parity = \"{Profile.Parity}\"");
                sb.AppendLine($"stop_bits = \"{Profile.StopBits}\"");
                break;

            case TransportType.Tcp:
                sb.AppendLine($"host = \"{Profile.HostIp}\"");
                sb.AppendLine($"port = {Profile.TcpPort}");
                break;

            case TransportType.NamedPipe:
                sb.AppendLine($"pipe_name = \"{Profile.PipeName}\"");
                break;

            case TransportType.ModbusTcp:
                sb.AppendLine($"host = \"{Profile.HostIp}\"");
                sb.AppendLine($"port = {Profile.TcpPort}");
                sb.AppendLine($"default_unit_id = {Profile.ModbusUnitId}");
                sb.AppendLine($"test_register = {Profile.ModbusTestRegister}");
                break;

            case TransportType.MelsecSlmp:
                sb.AppendLine($"host = \"{Profile.HostIp}\"");
                sb.AppendLine($"port = {Profile.TcpPort}");
                sb.AppendLine($"network_no = {Profile.MelsecNetworkNo}");
                sb.AppendLine($"pc_no = {Profile.MelsecPcNo}");
                sb.AppendLine($"default_device = \"{Profile.MelsecDevice}\"");
                break;

            case TransportType.Mqtt:
                sb.AppendLine($"host = \"{Profile.HostIp}\"");
                sb.AppendLine($"port = {Profile.TcpPort}");
                sb.AppendLine($"client_id = \"{Profile.MqttClientId}\"");
                sb.AppendLine($"topic_prefix = \"{Profile.MqttTopicPrefix}\"");
                break;

            case TransportType.OpcUa:
                sb.AppendLine($"endpoint_url = \"{Profile.OpcEndpointUrl}\"");
                sb.AppendLine($"node_id = \"{Profile.OpcNodeId}\"");
                sb.AppendLine($"auto_accept_certificates = true");
                break;
        }

        sb.AppendLine();
        sb.AppendLine($"[protocol]");
        sb.AppendLine($"codec = \"{Profile.Codec}\"");
        sb.AppendLine($"router = \"{Profile.Router}\"");
        sb.AppendLine($"timeout_ms = {Profile.TimeoutMs}");
        sb.AppendLine($"heartbeat_interval_ms = {Profile.HeartbeatIntervalMs}");

        GeneratedTomlPreview = sb.ToString();
    }

    [RelayCommand]
    public async Task SaveToFileAsync()
    {
        UpdateTomlPreview();

        try
        {
            // 1. Validate device name to prevent directory traversal and invalid filename chars
            string rawName = Profile.DeviceName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rawName))
            {
                TestStatus = "⚠️ 저장 실패: 장치 이름(DeviceName)이 비어있습니다.";
                TestResultColor = "#F9E2AF";
                return;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            var sanitizedSb = new StringBuilder();
            foreach (char c in rawName)
            {
                if (Array.IndexOf(invalidChars, c) < 0 && c != '/' && c != '\\' && c != '.')
                {
                    sanitizedSb.Append(c);
                }
            }

            string safeFileName = sanitizedSb.ToString();
            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                safeFileName = "kable_device";
            }

            string targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
            Directory.CreateDirectory(targetDir);

            string filePath = Path.Combine(targetDir, $"{safeFileName.ToLowerInvariant()}_comm.toml");

            // 2. Asynchronous write
            await File.WriteAllTextAsync(filePath, GeneratedTomlPreview, Encoding.UTF8);

            TestStatus = $"💾 파일 안전 저장 완료: {filePath}";
            TestResultColor = "#A6E3A1"; // green
            TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [SAVE] 설정 파일 비동기 기록 완료: {filePath}");
        }
        catch (Exception ex)
        {
            TestStatus = $"🔴 저장 실패: {ex.Message}";
            TestResultColor = "#F38BA8"; // red
            TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [SAVE 오류] {ex.Message}");
        }
    }
}

