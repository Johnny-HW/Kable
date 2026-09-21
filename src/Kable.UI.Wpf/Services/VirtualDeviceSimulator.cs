namespace Kable.UI.Wpf.Services;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;
using Kable.Observability;
using Kable.UI.Wpf.Models;
using Kable.UI.Wpf.ViewModels;

public sealed class VirtualDeviceSimulator : IDisposable
{
    private readonly CommTerminalViewModel _terminal;
    private readonly Func<IEnumerable<PacketCatalogItem>> _catalogProvider;
    private readonly Func<string> _deviceIdProvider;
    private readonly Random _random = new();

    private CancellationTokenSource? _cts;
    private Task? _simulationTask;

    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

    public event Action<bool>? StateChanged;

    public VirtualDeviceSimulator(CommTerminalViewModel terminal, Func<IEnumerable<PacketCatalogItem>> catalogProvider, Func<string> deviceIdProvider)
    {
        _terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
        _catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
        _deviceIdProvider = deviceIdProvider ?? throw new ArgumentNullException(nameof(deviceIdProvider));
    }

    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _simulationTask = Task.Run(() => RunSimulationLoopAsync(token), token);
        StateChanged?.Invoke(true);
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _cts?.Cancel();
        try
        {
            _simulationTask?.Wait(500);
        }
        catch
        {
            // ignore cancellation
        }
        _cts?.Dispose();
        _cts = null;
        StateChanged?.Invoke(false);
    }

    public void Toggle()
    {
        if (IsRunning) Stop();
        else Start();
    }

    private async Task RunSimulationLoopAsync(CancellationToken token)
    {
        var itemLastSent = new Dictionary<string, DateTime>();

        while (!token.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var deviceId = _deviceIdProvider();
            var catalog = _catalogProvider();

            foreach (var item in catalog)
            {
                if (!item.IsEnabled) continue;

                // 1. 상시 텔레메트리 (Periodic Telemetry)
                if (item.Kind == TrafficKind.PeriodicTelemetry)
                {
                    int interval = Math.Max(100, item.IntervalMs);
                    if (!itemLastSent.TryGetValue(item.Id, out var last) || (now - last).TotalMilliseconds >= interval)
                    {
                        itemLastSent[item.Id] = now;

                        // 노이즈가 가미된 시뮬레이션 값 계산
                        double noise = (_random.NextDouble() - 0.5) * 0.8;
                        double simulatedVal = Math.Round(item.SimulatedBaseValue + noise, 2);

                        string textPayload = $"{simulatedVal}";
                        var raw = Encoding.UTF8.GetBytes($"{item.Name}: {simulatedVal} {item.Unit}");

                        _terminal.OnPacketTrace(new PacketTraceRecord(
                            now,
                            PacketDirection.Rx,
                            TrafficKind.PeriodicTelemetry,
                            item.Name,
                            raw,
                            textPayload,
                            TimeSpan.FromMilliseconds(_random.Next(1, 4)),
                            LogLevel.Information,
                            deviceId));
                    }
                }
            }

            // 간헐적 가상 알람 시뮬레이션 (약 1/120 확률, 약 10~15초에 한 번)
            if (_random.Next(0, 120) == 7)
            {
                var almText = "ALM_WARN: Flow Sensor Jitter detected";
                var almBytes = Encoding.UTF8.GetBytes(almText);
                _terminal.OnPacketTrace(new PacketTraceRecord(
                    now,
                    PacketDirection.Rx,
                    TrafficKind.SpontaneousAlarm,
                    "FLOW_JITTER",
                    almBytes,
                    almText,
                    TimeSpan.Zero,
                    LogLevel.Warning,
                    deviceId));
            }

            try
            {
                await Task.Delay(100, token); // 10Hz 부드러운 틱 주기 (적당하고 안정적인 스트리밍 속도)
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
