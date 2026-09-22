namespace Kable.Tests.Cases.Security;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Kable.Core.Security;
using Kable.Core.Security.Guards;
using Xunit;

public sealed class LaunchGuardTests : IDisposable
{
    private readonly string _tempDirectory;

    public LaunchGuardTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "Kable_LaunchGuardTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    [Theory]
    [InlineData("run.bat")]
    [InlineData("script.cmd")]
    [InlineData("hack.ps1")]
    [InlineData("evil.vbs")]
    [InlineData("deploy.sh")]
    public async Task Validate_DisallowedExtensions_ThrowsSecurityValidationException(string fileName)
    {
        var guard = new PathAndHashLaunchGuard(new LaunchGuardOptions
        {
            EnableAllowlistOnly = false
        });

        var filePath = Path.Combine(_tempDirectory, fileName);
        var ex = await Assert.ThrowsAsync<SecurityValidationException>(async () =>
        {
            await guard.ValidateAndNormalizeAsync(filePath);
        });

        Assert.Contains("strictly prohibited", ex.Message);
    }

    [Theory]
    [InlineData("arg1 & evil.exe")]
    [InlineData("arg1 | evil.exe")]
    [InlineData("arg1; rm -rf /")]
    [InlineData("`whoami`")]
    [InlineData("$env:PATH")]
    [InlineData("output > secret.txt")]
    public async Task Validate_ShellInjectionArguments_ThrowsSecurityValidationException(string maliciousArgs)
    {
        var guard = new PathAndHashLaunchGuard();
        var safeExe = Path.Combine(_tempDirectory, "app.exe");

        var ex = await Assert.ThrowsAsync<SecurityValidationException>(async () =>
        {
            await guard.ValidateAndNormalizeAsync(safeExe, maliciousArgs);
        });

        Assert.Contains("shell injection", ex.Message);
    }

    [Fact]
    public async Task Validate_PathTraversal_ThrowsWhenOutsideAllowedDirectory()
    {
        var subDir = Path.Combine(_tempDirectory, "safe_bin");
        Directory.CreateDirectory(subDir);

        var guard = new PathAndHashLaunchGuard(new LaunchGuardOptions
        {
            EnableAllowlistOnly = true,
            AllowedDirectories = { subDir }
        });

        // 상대 경로 트래버설을 통해 상위 폴더의 파일을 지정
        var traversalPath = Path.Combine(subDir, "..", "outside.exe");

        var ex = await Assert.ThrowsAsync<SecurityValidationException>(async () =>
        {
            await guard.ValidateAndNormalizeAsync(traversalPath);
        });

        Assert.Contains("not within any allowed execution directory", ex.Message);
    }

    [Fact]
    public async Task Validate_AllowedExecutableNames_RejectsUnlistedExecutable()
    {
        var guard = new PathAndHashLaunchGuard(new LaunchGuardOptions
        {
            EnableAllowlistOnly = true,
            AllowedDirectories = { _tempDirectory },
            AllowedExecutableNames = { "Kable.Worker.exe", "ffmpeg.exe" }
        });

        var unlistedExe = Path.Combine(_tempDirectory, "unknown_tool.exe");
        var ex = await Assert.ThrowsAsync<SecurityValidationException>(async () =>
        {
            await guard.ValidateAndNormalizeAsync(unlistedExe);
        });

        Assert.Contains("not in the allowed executables list", ex.Message);
    }

    [Fact]
    public async Task Validate_AllowedExecutable_SucceedsAndNormalizesPath()
    {
        var guard = new PathAndHashLaunchGuard(new LaunchGuardOptions
        {
            EnableAllowlistOnly = true,
            AllowedDirectories = { _tempDirectory },
            AllowedExecutableNames = { "Kable.Worker.exe" }
        });

        var targetExe = Path.Combine(_tempDirectory, "Kable.Worker.exe");
        var normalized = await guard.ValidateAndNormalizeAsync(targetExe, "--port 9000");

        Assert.Equal(Path.GetFullPath(targetExe), normalized);
    }

    [Fact]
    public async Task Validate_Sha256Hash_PassesWhenHashMatches_FailsWhenTampered()
    {
        var exePath = Path.Combine(_tempDirectory, "secure_tool.exe");
        var originalBytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        await File.WriteAllBytesAsync(exePath, originalBytes);

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(originalBytes);
        var expectedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        var guard = new PathAndHashLaunchGuard(new LaunchGuardOptions
        {
            EnableAllowlistOnly = true,
            AllowedDirectories = { _tempDirectory },
            AllowedSha256Hashes = { ["secure_tool.exe"] = expectedHash }
        });

        // 1. 원본 해시와 일치하므로 통과
        var validatedPath = await guard.ValidateAndNormalizeAsync(exePath);
        Assert.Equal(Path.GetFullPath(exePath), validatedPath);

        // 2. 파일 변조(Tampering) 발생
        var tamperedBytes = new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 };
        await File.WriteAllBytesAsync(exePath, tamperedBytes);

        var ex = await Assert.ThrowsAsync<SecurityValidationException>(async () =>
        {
            await guard.ValidateAndNormalizeAsync(exePath);
        });

        Assert.Contains("Binary hash mismatch", ex.Message);
    }
}
