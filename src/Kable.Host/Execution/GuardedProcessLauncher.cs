namespace Kable.Host.Execution;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core.Security;

/// <summary>
/// ILaunchGuard(경로 및 악성 파일 검사)와 IConcurrencyGuard(중복 실행 방지)를 통합한 안전한 프로세스 런처 구현체입니다.
/// </summary>
public sealed class GuardedProcessLauncher : IProcessLauncher
{
    private static readonly HashSet<string> ProhibitedEnvironmentVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "PATH",
        "PATHEXT",
        "COMSPEC",
        "DOTNET_STARTUP_HOOKS",
        "DOTNET_ADDITIONAL_DEPS",
        "COR_ENABLE_PROFILING",
        "COR_PROFILER",
        "COR_PROFILER_PATH",
        "CORECLR_ENABLE_PROFILING",
        "CORECLR_PROFILER",
        "CORECLR_PROFILER_PATH",
        "LD_PRELOAD",
        "LD_LIBRARY_PATH",
        "DYLD_INSERT_LIBRARIES",
        "DYLD_LIBRARY_PATH",
        "PYTHONPATH",
        "NODE_OPTIONS"
    };

    private readonly ILaunchGuard _launchGuard;
    private readonly IConcurrencyGuard _concurrencyGuard;

    public GuardedProcessLauncher(ILaunchGuard launchGuard, IConcurrencyGuard concurrencyGuard)
    {
        _launchGuard = launchGuard ?? throw new ArgumentNullException(nameof(launchGuard));
        _concurrencyGuard = concurrencyGuard ?? throw new ArgumentNullException(nameof(concurrencyGuard));
    }

    public async ValueTask<Process> LaunchAsync(LaunchCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        ValidateEnvironmentVariables(command.EnvironmentVariables);
        var normalizedWorkingDir = ValidateWorkingDirectory(command.WorkingDirectory);

        // 1. 보안 가드를 통한 경로 정규화, Allowlist, 해시, 쉘 인젝션 검증
        var safeFullPath = await _launchGuard.ValidateAndNormalizeAsync(
            command.ExecutablePath,
            command.Arguments,
            ct).ConfigureAwait(false);

        // 2. 동시성 가드를 통한 프로세스 중복 실행(Busy 락) 제어
        var lockHandle = await _concurrencyGuard.TryAcquireAsync(safeFullPath, ct).ConfigureAwait(false);
        if (lockHandle == null)
        {
            throw new DeviceBusyException(safeFullPath, $"Executable '{Path.GetFileName(safeFullPath)}' is currently running or locked.");
        }

        int lockReleased = 0;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = safeFullPath,
                Arguments = command.Arguments ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = command.RedirectStandardStreams,
                RedirectStandardError = command.RedirectStandardStreams
            };

            if (!string.IsNullOrEmpty(normalizedWorkingDir))
            {
                startInfo.WorkingDirectory = normalizedWorkingDir;
            }

            if (command.EnvironmentVariables != null)
            {
                foreach (var kvp in command.EnvironmentVariables)
                {
                    startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                }
            }

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            // 프로세스 종료 시 비-async-void 안전 래퍼로 락 해제
            process.Exited += (_, _) =>
            {
                if (Interlocked.Exchange(ref lockReleased, 1) == 0)
                {
                    _ = DisposeLockSafeAsync(lockHandle);
                }
            };

            if (!process.Start())
            {
                if (Interlocked.Exchange(ref lockReleased, 1) == 0)
                {
                    await lockHandle.DisposeAsync().ConfigureAwait(false);
                }
                process.Dispose();
                throw new InvalidOperationException($"Failed to start process '{safeFullPath}'.");
            }

            if (process.HasExited && Interlocked.Exchange(ref lockReleased, 1) == 0)
            {
                _ = DisposeLockSafeAsync(lockHandle);
            }

            return process;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuardedProcessLauncher] Launch failed: {ex.Message}");
            if (Interlocked.Exchange(ref lockReleased, 1) == 0)
            {
                await lockHandle.DisposeAsync().ConfigureAwait(false);
            }
            throw;
        }
    }

    private static string? ValidateWorkingDirectory(string? workingDirectory)
    {
        if (string.IsNullOrEmpty(workingDirectory))
        {
            return null;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(workingDirectory);
        }
        catch (Exception ex)
        {
            throw new SecurityValidationException(workingDirectory, $"Invalid working directory format: {ex.Message}", ex);
        }

        if (!Directory.Exists(fullPath))
        {
            throw new SecurityValidationException(fullPath, $"Working directory '{fullPath}' does not exist.");
        }

        return fullPath;
    }

    private static void ValidateEnvironmentVariables(IReadOnlyDictionary<string, string>? environmentVariables)
    {
        if (environmentVariables == null || environmentVariables.Count == 0)
        {
            return;
        }

        foreach (var kvp in environmentVariables)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key) || ProhibitedEnvironmentVariables.Contains(kvp.Key.Trim()))
            {
                throw new SecurityValidationException(kvp.Key ?? string.Empty,
                    $"Overriding prohibited environment variable '{kvp.Key}' is not allowed.");
            }
        }
    }

    private static async Task DisposeLockSafeAsync(IAsyncDisposable lockHandle)
    {
        try
        {
            await lockHandle.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Debug.WriteLine($"[GuardedProcessLauncher] Lock release after process exit failed: {ex.Message}");
        }
    }
}
