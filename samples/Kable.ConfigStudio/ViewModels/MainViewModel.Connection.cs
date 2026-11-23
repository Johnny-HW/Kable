namespace Kable.ConfigStudio.ViewModels;

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Kable.Core;
using Kable.Observability;
using Kable.UI.Wpf.Models;
using Kable.UI.Wpf.Services;

public partial class MainViewModel
{
    private void InitializeTerminalAndCatalog()
    {
        Terminal.ManualSendRequested += HandleManualSendRequestedAsync;

        var defaultCommands = new[]
        {
            new Kable.Protocol.ScheduledCommandItem(
                new Kable.Protocol.CommandDefinition("REQ_STATUS", "Device Status Query", "GET_STATUS", "STATUS: READY"),
                Kable.Protocol.CommandExecutionMode.Aperiodic),
            new Kable.Protocol.ScheduledCommandItem(
                new Kable.Protocol.CommandDefinition("SET_TARGET_TEMP", "Target Temperature Control (Chamber)", "SET TEMP_TARGET", "ACK: TEMP_TARGET UPDATED", "°C", 45.0, 10.0, 120.0, 0.5, 10.0),
                Kable.Protocol.CommandExecutionMode.Aperiodic),
            new Kable.Protocol.ScheduledCommandItem(
                new Kable.Protocol.CommandDefinition("SET_PRESS_LIMIT", "Supply Pressure Limit", "SET PRESS_LIMIT", "ACK: PRESS_LIMIT UPDATED", "kPa", 150.0, 50.0, 300.0, 1.0, 1.0),
                Kable.Protocol.CommandExecutionMode.Aperiodic),
            new Kable.Protocol.ScheduledCommandItem(
                new Kable.Protocol.CommandDefinition("SET_FLOW_OFFSET", "Chemical Flow Offset", "SET FLOW_OFFSET", "ACK: FLOW_OFFSET APPLIED", "mL/min", 1.5, -5.0, 10.0, 0.1, 1.0),
                Kable.Protocol.CommandExecutionMode.Aperiodic),
            new Kable.Protocol.ScheduledCommandItem(
                new Kable.Protocol.CommandDefinition("CHAMBER_TEMP", "Chamber Temperature", "", "TEMP:{VAL}", "°C", 24.8),
                Kable.Protocol.CommandExecutionMode.Periodic, 800),
            new Kable.Protocol.ScheduledCommandItem(
                new Kable.Protocol.CommandDefinition("LINE_PRESSURE", "Line Supply Pressure", "", "PRESS:{VAL}", "kPa", 101.3),
                Kable.Protocol.CommandExecutionMode.Periodic, 1000),
            new Kable.Protocol.ScheduledCommandItem(
                new Kable.Protocol.CommandDefinition("FLOW_RATE", "Chemical Flow Rate", "", "FLOW:{VAL}", "mL/min", 12.5),
                Kable.Protocol.CommandExecutionMode.Periodic, 600),
            new Kable.Protocol.ScheduledCommandItem(
                new Kable.Protocol.CommandDefinition("ALM_OVERTEMP", "Overtemp Detected Alarm", "", "ALM_001: OVERTEMP"),
                Kable.Protocol.CommandExecutionMode.Aperiodic)
        };

        PacketCatalog.Clear();
        foreach (var sc in defaultCommands)
        {
            var item = PacketCatalogItem.FromScheduledItem(sc);
            AttachCatalogItemSync(item);
            PacketCatalog.Add(item);
        }

        Terminal.CommandConsole.LoadScheduledCommands(defaultCommands);

        RefreshComPorts();
        UpdateTomlPreview();

        _simulator = new VirtualDeviceSimulator(
            Terminal,
            () => PacketCatalog,
            () => Profile.DeviceName);

        Terminal.TelemetryUpdated += HandleTelemetryUpdated;
        Terminal.AlarmList.Alarms.CollectionChanged += (_, _) =>
        {
            int count = Terminal.AlarmList.Alarms.Count;
            ActiveFaultsSummary = $"{count} Warning, 0 Crit";
        };
    }

    private void AttachCatalogItemSync(PacketCatalogItem item)
    {
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PacketCatalogItem.Kind)
                or nameof(PacketCatalogItem.IsEnabled)
                or nameof(PacketCatalogItem.CommandPayload)
                or nameof(PacketCatalogItem.Name))
            {
                SyncCommandConsole();
            }
        };
    }

    private async Task HandleManualSendRequestedAsync(string cmd)
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

        int cmdLatencyMs = Random.Shared.Next(35, 65);
        await Task.Delay(cmdLatencyMs);

        string echoResp;
        if (cmd.StartsWith("SET TEMP_TARGET=", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(cmd["SET TEMP_TARGET=".Length..], CultureInfo.InvariantCulture, out double targetTemp))
        {
            var tempItem = PacketCatalog.FirstOrDefault(p => p.Id == "CHAMBER_TEMP");
            if (tempItem != null) tempItem.SimulatedBaseValue = targetTemp;
            echoResp = $"ACK: TEMP_TARGET UPDATED TO {targetTemp:F1}°C";
        }
        else if (cmd.StartsWith("SET PRESS_LIMIT=", StringComparison.OrdinalIgnoreCase) &&
                 double.TryParse(cmd["SET PRESS_LIMIT=".Length..], CultureInfo.InvariantCulture, out double pressLimit))
        {
            var pressItem = PacketCatalog.FirstOrDefault(p => p.Id == "LINE_PRESSURE");
            if (pressItem != null) pressItem.SimulatedBaseValue = pressLimit;
            echoResp = $"ACK: PRESS_LIMIT UPDATED TO {pressLimit:F1} kPa";
        }
        else if (cmd.StartsWith("SET FLOW_OFFSET=", StringComparison.OrdinalIgnoreCase) &&
                 double.TryParse(cmd["SET FLOW_OFFSET=".Length..], CultureInfo.InvariantCulture, out double flowOffset))
        {
            var flowItem = PacketCatalog.FirstOrDefault(p => p.Id == "FLOW_RATE");
            if (flowItem != null) flowItem.SimulatedBaseValue = Math.Round(12.5 + flowOffset, 2);
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
            TimeSpan.FromMilliseconds(cmdLatencyMs),
            LogLevel.Information,
            Profile.DeviceName));
    }

    private void HandleTelemetryUpdated(string tag, string val, string unit)
    {
        if (string.Equals(tag, "Chamber Temperature", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag, "CHAMBER_TEMP", StringComparison.OrdinalIgnoreCase))
        {
            LiveChamberTemp = $"{val} {unit}";
            if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double tVal))
            {
                LiveChamberThermalStatus = tVal > 60.0 ? $"Warning • {tVal:F1}°C" : $"Normal • {tVal:F1}°C";
                LiveChamberThermalProgress = Math.Clamp((tVal / 120.0) * 100.0, 0, 100);
            }
        }
        else if (string.Equals(tag, "Line Supply Pressure", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(tag, "LINE_PRESSURE", StringComparison.OrdinalIgnoreCase))
        {
            LiveLinePressure = $"{val} {unit}";
            if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double pVal))
            {
                LiveVacuumSealPa = $"{pVal / 8.0:F1} Pa";
                LiveVacuumProgress = Math.Clamp((pVal / 150.0) * 100.0, 0, 100);
            }
        }
        else if (string.Equals(tag, "Chemical Flow Rate", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(tag, "FLOW_RATE", StringComparison.OrdinalIgnoreCase))
        {
            LiveFlowRate = $"{val} {unit}";
        }
    }

    [RelayCommand]
    public void RefreshComPorts()
    {
        DetectedComPorts.Clear();
        try
        {
            foreach (string port in SerialPort.GetPortNames())
            {
                DetectedComPorts.Add(port);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or InvalidOperationException or Win32Exception)
        {
            Debug.WriteLine($"[MainViewModel] RefreshComPorts failed: {ex.Message}");
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

    [RelayCommand]
    public async Task RunLiveTestAsync()
    {
        TestStatus = $"⚡ [실제 진단] {Profile.Transport} 네트워크 링크 핸드셰이크 진행 중...";
        TestResultColor = "#F9E2AF";
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
                TestResultColor = "#A6E3A1";
            }
            else if (Profile.Transport == TransportType.NamedPipe)
            {
                TraceLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] [LIVE] Named Pipe 확인: \\\\.\\pipe\\{Profile.PipeName}");
                bool exists = _fileService.NamedPipeExists(Profile.PipeName);
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
            TestResultColor = "#F38BA8";
        }
    }
}
