namespace Kable.UI.Wpf.Tests;

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kable.ConfigStudio.ViewModels;
using Kable.Core;
using Kable.Localization;
using Kable.Observability;
using Kable.UI.Wpf.Controls;
using Kable.UI.Wpf.Localization;
using Kable.UI.Wpf.Models;
using Kable.UI.Wpf.Services;
using Kable.UI.Wpf.ViewModels;
using Xunit;

public sealed class WpfViewModelTests
{
    [Fact]
    public void CommTerminal_PeriodicTelemetry_LogsOnceAndPropagatesUnit()
    {
        StaTestHelper.Run(dispatcher =>
        {
            using var terminal = new CommTerminalViewModel(dispatcher);
            string? capturedTag = null;
            string? capturedVal = null;
            string? capturedUnit = null;
            terminal.TelemetryUpdated += (tag, val, unit) =>
            {
                capturedTag = tag;
                capturedVal = val;
                capturedUnit = unit;
            };

            var raw = Encoding.UTF8.GetBytes("Chamber Temperature: 24.8 °C");
            terminal.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow, PacketDirection.Rx, TrafficKind.PeriodicTelemetry,
                "Chamber Temperature", raw, "24.8", TimeSpan.FromMilliseconds(2), LogLevel.Information, "DEV1"));

            terminal.TelemetryStream.RawLogStream.FlushPending();
            StaTestHelper.DrainDispatcher(dispatcher);

            Assert.Single(terminal.TelemetryStream.RawLogStream.Packets);
            Assert.Single(terminal.TelemetryStream.TelemetryItems);
            Assert.Equal("°C", terminal.TelemetryStream.TelemetryItems[0].Unit);
            Assert.Equal("Chamber Temperature", capturedTag);
            Assert.Equal("24.8", capturedVal);
            Assert.Equal("°C", capturedUnit);
        });
    }

    [Fact]
    public void TelemetryStream_FilterTextFiltersView_AndClearDropsStaleQueuedAdd()
    {
        StaTestHelper.Run(dispatcher =>
        {
            using var vm = new TelemetryStreamViewModel(dispatcher);
            vm.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow, PacketDirection.Rx, TrafficKind.PeriodicTelemetry,
                "Chamber Temperature", Encoding.UTF8.GetBytes("Chamber Temperature: 24.8 °C"), "24.8", TimeSpan.Zero, LogLevel.Information, "DEV1"));
            vm.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow, PacketDirection.Rx, TrafficKind.PeriodicTelemetry,
                "Line Pressure", Encoding.UTF8.GetBytes("Line Pressure: 101.3 kPa"), "101.3", TimeSpan.Zero, LogLevel.Information, "DEV1"));

            vm.FilterText = "Pressure";
            var filtered = vm.TelemetryView.Cast<TelemetryItemModel>().ToList();
            Assert.Single(filtered);
            Assert.Equal("Line Pressure", filtered[0].ParameterName);

            vm.FilterText = string.Empty;
            Assert.Equal(2, vm.TelemetryView.Cast<TelemetryItemModel>().Count());

            // Enqueue from background thread then immediately ClearTelemetry before dispatcher pumps.
            Task.Run(() => vm.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow, PacketDirection.Rx, TrafficKind.PeriodicTelemetry,
                "Flow Rate", Encoding.UTF8.GetBytes("Flow Rate: 12.5 mL/min"), "12.5", TimeSpan.Zero, LogLevel.Information, "DEV1"))).GetAwaiter().GetResult();
            vm.ClearTelemetry();
            StaTestHelper.DrainDispatcher(dispatcher);

            Assert.Empty(vm.TelemetryItems);
        });
    }

    [Fact]
    public void AlarmList_FilterTextFiltersView_AndClearRemovesItems()
    {
        StaTestHelper.Run(dispatcher =>
        {
            var vm = new AlarmListViewModel(dispatcher);
            vm.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                "ALM_TEMP", Encoding.UTF8.GetBytes("High temp"), "High temp", TimeSpan.Zero, LogLevel.Critical, "DEV1"));
            vm.OnPacketTrace(new PacketTraceRecord(
                DateTime.UtcNow, PacketDirection.Rx, TrafficKind.SpontaneousAlarm,
                "ALM_FLOW", Encoding.UTF8.GetBytes("Low flow"), "Low flow", TimeSpan.Zero, LogLevel.Warning, "DEV2"));

            vm.FilterText = "CRITICAL";
            var filtered = vm.AlarmsView.Cast<AlarmDisplayModel>().ToList();
            Assert.Single(filtered);
            Assert.Equal("ALM_TEMP", filtered[0].AlarmCode);

            vm.ClearAlarms();
            Assert.Empty(vm.Alarms);
        });
    }

    [Fact]
    public void RawPacketLogStream_BatchesFlush_CapsAtMax_AndRespectsFilterSnapshot()
    {
        StaTestHelper.Run(dispatcher =>
        {
            using var vm = new RawPacketLogStreamViewModel(dispatcher, maxLogCount: 5);
            vm.FilterText = "MATCH";

            for (int i = 0; i < 10; i++)
            {
                string payload = i % 2 == 0 ? $"MATCH_{i}" : $"SKIP_{i}";
                vm.OnPacketTrace(new PacketTraceRecord(
                    DateTime.UtcNow, PacketDirection.Rx, TrafficKind.PeriodicTelemetry,
                    "TAG", Encoding.UTF8.GetBytes(payload), payload, TimeSpan.FromMilliseconds(1), LogLevel.Information, "DEV1"));
            }

            // Before FlushPending, UI collection is not flooded synchronously.
            Assert.Empty(vm.Packets);

            vm.FlushPending();
            Assert.Equal(5, vm.Packets.Count);
            Assert.All(vm.Packets, p => Assert.Contains("MATCH_", p.AsciiPreview, StringComparison.Ordinal));
            Assert.Equal("MATCH_8", vm.Packets[0].AsciiPreview);

            vm.ClearLogs();
            Assert.Empty(vm.Packets);
        });
    }

    [Fact]
    public async Task VirtualDeviceSimulator_AppliesSpeedMultiplier_AndStopsNonBlocking()
    {
        using var terminal = new CommTerminalViewModel();
        var catalog = new ObservableCollection<PacketCatalogItem>
        {
            new() { Id = "T1", Name = "Chamber Temp", Kind = TrafficKind.PeriodicTelemetry, IntervalMs = 100, Unit = "°C", SimulatedBaseValue = 25.0, IsEnabled = true }
        };

        int emitted = 0;
        var gotTwo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.TelemetryUpdated += (_, _, _) =>
        {
            if (Interlocked.Increment(ref emitted) >= 2) gotTwo.TrySetResult();
        };

        await using var sim = new VirtualDeviceSimulator(terminal, () => catalog, () => "DEV1", catalogDispatcher: null);
        sim.SpeedMultiplier = 2.0;
        sim.Start();
        Assert.True(sim.IsRunning);

        await gotTwo.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var sw = Stopwatch.StartNew();
        sim.Stop();
        sw.Stop();
        Assert.False(sim.IsRunning);
        Assert.True(sw.ElapsedMilliseconds < 250, $"Stop() blocked for {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void LocKeyConverter_ResolvesKnownResxKeysAndFallsBackOnUnknown()
    {
        var previous = KableLocalizer.Instance.CurrentCulture;
        try
        {
            KableLocalizer.Instance.SetCulture(CultureInfo.GetCultureInfo("en-US"));
            var known = new LocKeyConverter("Btn_Clear");
            string enText = Assert.IsType<string>(known.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));
            Assert.NotEqual("Btn_Clear", enText);

            var unknown = new LocKeyConverter("NonExistent_Key_12345");
            string fallback = Assert.IsType<string>(unknown.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));
            Assert.Equal("NonExistent_Key_12345", fallback);
        }
        finally
        {
            KableLocalizer.Instance.SetCulture(previous);
        }
    }

    [Fact]
    public void ConfigStudio_MainViewModel_SanitizesStructName_DefaultsOpcUaSecure_AndDisposesCleanly()
    {
        StaTestHelper.Run(_ =>
        {
            using var vm = new MainViewModel();
            vm.Profile.DeviceName = "123 Pump-Main (A)";
            vm.SelectTransport(TransportType.OpcUa);

            Assert.Contains("public readonly struct _123PumpMainATelemetryPacket", vm.GeneratedCSharpPreview, StringComparison.Ordinal);
            Assert.Contains("auto_accept_certificates = false", vm.GeneratedTomlPreview, StringComparison.Ordinal);
            Assert.DoesNotContain("auto_accept_certificates = true", vm.GeneratedTomlPreview, StringComparison.Ordinal);

            vm.ToggleSimulation();
            Assert.True(vm.IsSimulatorRunning);
            vm.Dispose();
            Assert.False(vm.IsSimulatorRunning);
        });
    }

    [Fact]
    public void IosTheme_LoadsCleanly_AndAllBrushesAreFrozen()
    {
        StaTestHelper.Run(_ =>
        {
            var dict = new System.Windows.ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Kable.UI.Wpf;component/Themes/IosTheme.xaml", UriKind.Absolute)
            };

            Assert.True(dict.Contains("IosDataGridStyle") || dict.MergedDictionaries.Any(m => m.Contains("IosDataGridStyle")));
            var colors = dict.MergedDictionaries.First(m => m.Source?.OriginalString.EndsWith("IosTheme.Colors.xaml", StringComparison.OrdinalIgnoreCase) == true);
            var brushes = colors.Values.OfType<System.Windows.Media.SolidColorBrush>().ToList();
            Assert.NotEmpty(brushes);
            Assert.All(brushes, b => Assert.True(b.IsFrozen));
        });
    }
}
