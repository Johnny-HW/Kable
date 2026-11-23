using System.IO;

namespace Kable.ProtocolPack.Tests.Support;

internal static class PackTestFolder
{
    public static string FixturesRoot => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static string MassHunter => Get("MassHunter-7700");

    public static string AbbTalys => Get("ABB-TALYS");

    public static string LinePlc => Get("Line-PLC");

    public static string Get(string name) => Path.Combine(FixturesRoot, name);

    public static string Clone(string fixtureName)
    {
        var destination = Path.Combine(
            Path.GetTempPath(),
            "KableProtocolPackTests",
            Guid.NewGuid().ToString("N"),
            fixtureName);
        CopyDirectory(Get(fixtureName), destination);
        return destination;
    }

    public static TempPackScope CloneScoped(string fixtureName)
    {
        var root = Path.Combine(Path.GetTempPath(), "KableProtocolPackTests", Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(root, fixtureName);
        CopyDirectory(Get(fixtureName), destination);
        return new TempPackScope(root, destination);
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"[PackTestFolder] Temp cleanup skipped for '{path}': {ex.Message}");
        }
    }

    internal sealed class TempPackScope : IDisposable
    {
        private readonly string _root;
        public string Folder { get; }

        public TempPackScope(string root, string folder)
        {
            _root = root;
            Folder = folder;
        }

        public void Dispose() => TryDelete(_root);
    }

    public static void WriteAllText(string folder, string relativePath, string contents)
    {
        var path = Path.Combine(folder, relativePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, contents);
    }

    public static void ReplaceInFile(string folder, string relativePath, string oldValue, string newValue)
    {
        var path = Path.Combine(folder, relativePath);
        var yaml = File.ReadAllText(path);
        File.WriteAllText(path, yaml.Replace(oldValue, newValue));
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
