namespace Kable.Core.Security.Guards;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core.Security;

/// <summary>
/// 파일 경로 정규화, 허용 디렉터리/파일명 화이트리스트, 스크립트 확장자 차단,
/// 쉘 인젝션 방지 및 SHA-256 해시 검증을 수행하는 표준 LAUNCH 가드입니다.
/// </summary>
public sealed class PathAndHashLaunchGuard : ILaunchGuard
{
    private static readonly char[] ShellInjectionChars = ['&', '|', ';', '`', '$', '>', '<', '\n', '\r'];
    private readonly LaunchGuardOptions _options;

    public PathAndHashLaunchGuard(LaunchGuardOptions? options = null)
    {
        _options = options ?? new LaunchGuardOptions();
    }

    public async ValueTask<string> ValidateAndNormalizeAsync(string executablePath, string? arguments = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new SecurityValidationException(executablePath ?? string.Empty, "Executable path cannot be empty.");
        }

        // 1. 인자(Arguments) 쉘 인젝션 메타문자 검사
        if (_options.RestrictShellCharacters && !string.IsNullOrEmpty(arguments))
        {
            if (arguments!.IndexOfAny(ShellInjectionChars) >= 0)
            {
                throw new SecurityValidationException(arguments, "Command arguments contain prohibited shell injection characters.");
            }
        }

        // 2. 경로 정규화 (상대경로 및 ../ 디렉터리 트래버설 무력화)
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(executablePath);
        }
        catch (Exception ex)
        {
            throw new SecurityValidationException(executablePath, $"Invalid executable path format: {ex.Message}", ex);
        }

        var fileName = Path.GetFileName(fullPath);
        var extension = Path.GetExtension(fullPath);

        // 3. 위험 스크립트 확장자 차단 검사
        if (!string.IsNullOrEmpty(extension) && _options.DisallowedExtensions.Contains(extension))
        {
            throw new SecurityValidationException(fullPath, $"Execution of script extension '{extension}' is strictly prohibited.");
        }

        if (!_options.EnableAllowlistOnly)
        {
            return fullPath;
        }

        // 4. 허용 디렉터리(AllowedDirectories) 검사
        if (_options.AllowedDirectories.Count > 0)
        {
            bool directoryAllowed = false;
            foreach (var allowedDir in _options.AllowedDirectories)
            {
                var normalizedAllowedDir = Path.GetFullPath(allowedDir);
                if (!normalizedAllowedDir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    normalizedAllowedDir += Path.DirectorySeparatorChar;
                }

                if (fullPath.StartsWith(normalizedAllowedDir, StringComparison.OrdinalIgnoreCase))
                {
                    directoryAllowed = true;
                    break;
                }
            }

            if (!directoryAllowed)
            {
                throw new SecurityValidationException(fullPath, $"Path '{fullPath}' is not within any allowed execution directory.");
            }
        }

        // 5. 허용 실행 파일명(AllowedExecutableNames) 검사
        if (_options.AllowedExecutableNames.Count > 0)
        {
            if (!_options.AllowedExecutableNames.Contains(fileName))
            {
                throw new SecurityValidationException(fullPath, $"Executable '{fileName}' is not in the allowed executables list.");
            }
        }

        // 6. SHA-256 무결성 해시 검증
        if (_options.AllowedSha256Hashes.Count > 0)
        {
            string? expectedHash = null;
            if (_options.AllowedSha256Hashes.TryGetValue(fileName, out var hashByName))
            {
                expectedHash = hashByName;
            }
            else if (_options.AllowedSha256Hashes.TryGetValue(fullPath, out var hashByFullPath))
            {
                expectedHash = hashByFullPath;
            }

            if (!string.IsNullOrEmpty(expectedHash))
            {
                if (!File.Exists(fullPath))
                {
                    throw new SecurityValidationException(fullPath, $"Target executable file '{fullPath}' does not exist on disk for hash verification.");
                }

                var actualHash = await ComputeSha256Async(fullPath, ct).ConfigureAwait(false);
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new SecurityValidationException(fullPath, $"Binary hash mismatch for '{fullPath}'. Expected: {expectedHash}, Actual: {actualHash}");
                }
            }
        }

        return fullPath;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        using var sha256 = SHA256.Create();
#if NET6_0_OR_GREATER
        var hashBytes = await sha256.ComputeHashAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
#else
        var hashBytes = sha256.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
#endif
    }
}
