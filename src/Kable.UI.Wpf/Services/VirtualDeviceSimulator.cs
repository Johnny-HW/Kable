namespace Kable.UI.Wpf.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Kable.Core;
using Kable.Observability;
using Kable.UI.Wpf.Models;
using Kable.UI.Wpf.ViewModels;

public sealed class VirtualDeviceSimulator : IDisposable, IAsyncDisposable
{
    private readonly CommTerminalViewModel _terminal;
    private readonly Func<IEnumerable<PacketCatalogItem>> _catalogProvider;
    private readonly Func<string> _deviceIdProvider;
    private readonly Dispatcher? _catalogDispatcher;
    private readonly object _gate = new();
    private double _speedMultiplier = 1.0;

    private CancellationTokenSource? _cts;
    private Task? _simulationTask;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _cts != null && !_cts.IsCancellationRequested;
            }
        }
    }

    public double SpeedMultiplier
    {
        get => Volatile.Read(ref _speedMultiplier);
        set => Volatile.Write(ref _speedMultiplier, Math.Clamp(double.IsFinite(value) ? value : 1.0, 0.1, 10.0));
    }

    public event Action<bool>? StateChanged;

    public VirtualDeviceSimulator(
        CommTerminalViewModel terminal,
        Func<IEnumerable<PacketCatalogItem>> catalogProvider,
        Func<string> deviceIdProvider)
        : this(terminal, catalogProvider, deviceIdProvider, Application.Current?.Dispatcher)
    {
    }

    public VirtualDeviceSimulator(
        CommTerminalViewModel terminal,
        Func<IEnumerable<PacketCatalogItem>> catalogProvider,
        Func<string> deviceIdProvider,
        Dispatcher? catalogDispatcher)
    {
        _terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
        _catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
        _deviceIdProvider = deviceIdProvider ?? throw new ArgumentNullException(nameof(deviceIdProvider));
        _catalogDispatcher = catalogDispatcher;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_cts != null && !_cts.IsCancellationRequested) return;

            var cts = new CancellationTokenSource();
            var token = cts.Token;
            _cts = cts;
            _simulationTask = Task.Run(() => RunSimulationLoopAsync(cts, token), CancellationToken.None);
        }

        StateChanged?.Invoke(true);
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        lock (_gate)
        {
            if (_cts == null) return;
            cts = _cts;
            _cts = null;
            _simulationTask = null;
        }

        try { cts.Cancel(); }
        catch (ObjectDisposedException) { }
        StateChanged?.Invoke(false);
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? running;
        lock (_gate)
        {
            if (_cts == null && _simulationTask == null) return;
            cts = _cts;
            running = _simulationTask;
            _cts = null;
            _simulationTask = null;
        }

        if (cts != null)
        {
            try { cts.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        if (running != null)
        {
            try { await running.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        StateChanged?.Invoke(false);
    }

    public void Toggle()
    {
        if (IsRunning) Stop();
        else Start();
    }

    private async Task<PacketCatalogItem[]> SnapshotCatalogAsync(CancellationToken token)
    {
        if (_catalogDispatcher != null && !_catalogDispatcher.CheckAccess())
        {
            if (_catalogDispatcher.HasShutdownStarted) return Array.Empty<PacketCatalogItem>();
            try
            {
                return await _catalogDispatcher.InvokeAsync(
                    () => _catalogProvider().ToArray(),
                    DispatcherPriority.Background,
                    token);
            }
            catch (TaskCanceledException)
            {
                return Array.Empty<PacketCatalogItem>();
            }
        }

        return _catalogProvider().ToArray();
    }

    private async Task RunSimulationLoopAsync(CancellationTokenSource ownedCts, CancellationToken token)
    {
        var itemLastSent = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        try
        {
            while (!token.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                double speed = SpeedMultiplier;
                string deviceId = _deviceIdProvider();
                var catalog = await SnapshotCatalogAsync(token).ConfigureAwait(false);

                foreach (var item in catalog)
                {
                    if (token.IsCancellationRequested) break;
                    if (!item.IsEnabled) continue;

                    if (item.Kind == TrafficKind.PeriodicTelemetry)
                    {
                        double effectiveInterval = Math.Max(40.0, Math.Max(100, item.IntervalMs) / speed);
                        if (!itemLastSent.TryGetValue(item.Id, out var last) || (now - last).TotalMilliseconds >= effectiveInterval)
                        {
                            itemLastSent[item.Id] = now;

                            double noise = (Random.Shared.NextDouble() - 0.5) * 0.8;
                            double simulatedVal = Math.Round(item.SimulatedBaseValue + noise, 2);

                            string textPayload = simulatedVal.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            string rawLine = string.IsNullOrEmpty(item.Unit)
                                ? $"{item.Name}: {textPayload}"
                                : $"{item.Name}: {textPayload} {item.Unit}";
                            var raw = Encoding.UTF8.GetBytes(rawLine);

                            _terminal.OnPacketTrace(new PacketTraceRecord(
                                now,
                                PacketDirection.Rx,
                                TrafficKind.PeriodicTelemetry,
                                item.Name,
                                raw,
                                textPayload,
                                TimeSpan.FromMilliseconds(Random.Shared.Next(1, 4)),
                                LogLevel.Information,
                                deviceId));
                        }
                    }
                }

                if (!token.IsCancellationRequested && Random.Shared.Next(0, 120) == 7)
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

                int tickMs = Math.Clamp((int)Math.Round(100.0 / speed), 20, 250);
                try
                {
                    await Task.Delay(tickMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            ownedCts.Dispose();
        }
    }

    public void Dispose()
    {
        Stop();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }
}
