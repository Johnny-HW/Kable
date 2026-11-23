namespace Kable.ConfigStudio.Services;

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

public interface IConfigStudioFileService
{
    bool NamedPipeExists(string pipeName);
    Task<string> SaveGeneratedDriverAsync(string rawDeviceName, string csharpContent);
}

public sealed class ConfigStudioFileService : IConfigStudioFileService
{
    public static ConfigStudioFileService Instance { get; } = new();

    public bool NamedPipeExists(string pipeName)
    {
        string pipePath = $@"\\.\pipe\{pipeName}";
        return File.Exists(pipePath);
    }

    public async Task<string> SaveGeneratedDriverAsync(string rawDeviceName, string csharpContent)
    {
        ArgumentNullException.ThrowIfNull(rawDeviceName);
        ArgumentNullException.ThrowIfNull(csharpContent);

        char[] invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedSb = new StringBuilder();
        foreach (char c in rawDeviceName)
        {
            if (Array.IndexOf(invalidChars, c) < 0 && c != '/' && c != '\\' && c != '.')
            {
                sanitizedSb.Append(c);
            }
        }

        string safeFileName = sanitizedSb.ToString();
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = "kable_device";
        }

        string targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GeneratedDrivers");
        Directory.CreateDirectory(targetDir);

        string filePath = Path.Combine(targetDir, $"{safeFileName}TelemetryPacket.cs");
        await File.WriteAllTextAsync(filePath, csharpContent, Encoding.UTF8);
        return filePath;
    }
}
