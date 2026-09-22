namespace Kable.Host.Execution;

using System;
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

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = safeFullPath,
                Arguments = command.Arguments ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            if (!string.IsNullOrEmpty(command.WorkingDirectory))
            {
                startInfo.WorkingDirectory = command.WorkingDirectory;
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

            // 프로세스 종료 시 락 자동 해제
            process.Exited += async (s, e) =>
            {
                await lockHandle.DisposeAsync().ConfigureAwait(false);
            };

            if (!process.Start())
            {
                await lockHandle.DisposeAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"Failed to start process '{safeFullPath}'.");
            }

            return process;
        }
        catch
        {
            await lockHandle.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
