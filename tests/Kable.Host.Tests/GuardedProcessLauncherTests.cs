namespace Kable.Host.Tests;

using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Core.Security;
using Kable.Core.Security.Guards;
using Kable.Host.Execution;
using Xunit;

public sealed class GuardedProcessLauncherTests : IDisposable
{
    private readonly string _tempDir;

    public GuardedProcessLauncherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Kable_LauncherTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch
            {
                // Best-effort
            }
        }
    }

    [Fact]
    public async Task LaunchAsync_MaliciousScriptExtension_ThrowsSecurityValidationException()
    {
        var launchGuard = new PathAndHashLaunchGuard(new LaunchGuardOptions
        {
            EnableAllowlistOnly = false
        });
        var concurrencyGuard = new SingleFlightConcurrencyGuard();
        var launcher = new GuardedProcessLauncher(launchGuard, concurrencyGuard);

        var badCommand = new LaunchCommand
        {
            ExecutablePath = Path.Combine(_tempDir, "malicious.bat")
        };

        var act = async () => await launcher.LaunchAsync(badCommand);
        await Assert.ThrowsAsync<SecurityValidationException>(act);
    }

    [Fact]
    public async Task LaunchAsync_ShellInjectionArguments_ThrowsSecurityValidationException()
    {
        var launchGuard = new PathAndHashLaunchGuard();
        var concurrencyGuard = new SingleFlightConcurrencyGuard();
        var launcher = new GuardedProcessLauncher(launchGuard, concurrencyGuard);

        var badCommand = new LaunchCommand
        {
            ExecutablePath = Path.Combine(_tempDir, "safe.exe"),
            Arguments = "run & echo hacked"
        };

        var act = async () => await launcher.LaunchAsync(badCommand);
        var ex = await Assert.ThrowsAsync<SecurityValidationException>(act);
        ex.Message.Should().Contain("shell injection");
    }

    [Fact]
    public async Task LaunchAsync_WhenAlreadyRunning_ThrowsDeviceBusyException()
    {
        var launchGuard = new PathAndHashLaunchGuard(new LaunchGuardOptions
        {
            EnableAllowlistOnly = false
        });
        var concurrencyGuard = new SingleFlightConcurrencyGuard(new ConcurrencyOptions
        {
            Mode = BusyHandlingMode.RejectImmediately
        });
        var launcher = new GuardedProcessLauncher(launchGuard, concurrencyGuard);

        var targetFile = Path.Combine(_tempDir, "dummy_worker.exe");
        var normalized = Path.GetFullPath(targetFile);

        // 이미 락이 잡혀 있는 상황 시뮬레이션
        var handle = await concurrencyGuard.AcquireAsync(normalized);

        var cmd = new LaunchCommand
        {
            ExecutablePath = targetFile
        };

        var act = async () => await launcher.LaunchAsync(cmd);
        var ex = await Assert.ThrowsAsync<DeviceBusyException>(act);
        ex.ResourceKey.Should().Be(normalized);

        await handle.DisposeAsync();
    }
}
