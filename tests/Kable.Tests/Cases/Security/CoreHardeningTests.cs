namespace Kable.Tests.Cases.Security;

using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Core.Security;
using Kable.Core.Security.Guards;
using Kable.Localization;
using Kable.Observability;
using Xunit;

public sealed class CoreHardeningTests
{
    [Fact]
    public async Task KableLocalizer_ConcurrentRegisterAndRead_IsThreadSafe()
    {
        var localizer = new KableLocalizer();
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2));

        var writer = Task.Run(() =>
        {
            for (int i = 0; i < 500; i++)
            {
                localizer.RegisterCustomString("ko", $"KEY_{i % 32}", $"VAL_{i}");
                localizer.RegisterCustomError("ko", KableErrorCode.DeviceTimeout, $"TO_{i}");
            }
        });

        var reader = Task.Run(() =>
        {
            for (int i = 0; i < 500; i++)
            {
                _ = localizer.GetString($"KEY_{i % 32}");
                _ = localizer.GetErrorMessage(KableErrorCode.DeviceTimeout);
            }
        });

        await Task.WhenAll(writer, reader).WaitAsync(cts.Token);
    }

    [Fact]
    public async Task SingleFlightConcurrencyGuard_DisposeWhileLockHeld_ReleaserDoesNotThrow()
    {
        var guard = new SingleFlightConcurrencyGuard(new ConcurrencyOptions
        {
            Mode = BusyHandlingMode.EnqueueFifo
        });

        var lease = await guard.AcquireAsync("dynamic-key-1");
        await guard.DisposeAsync();

        // Releasing after guard disposal must cleanly dispose the deferred semaphore without throwing.
        await lease.DisposeAsync();
    }

    [Fact]
    public async Task PcapWriter_NormalizesLocalTimestampAndDisposeIsIdempotent()
    {
        using var ms = new MemoryStream();
        var writer = new PcapWriter(ms);

        var localTime = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Local);
        await writer.WritePacketAsync(new byte[] { 1, 2, 3, 4 }, localTime);

        await writer.DisposeAsync();
        await writer.DisposeAsync(); // idempotent

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await writer.WritePacketAsync(new byte[] { 5 }, DateTime.UtcNow));
    }
}
