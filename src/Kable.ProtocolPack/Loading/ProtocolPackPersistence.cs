using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Kable.ProtocolPack.Models;

namespace Kable.ProtocolPack.Loading;

internal static class ProtocolPackPersistence
{
    public static void Save(string folder, ProtocolPackDocument document)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, ProtocolPackLoader.ProtocolFileName),
            ProtocolPackYaml.SerializeDocument(ToRoot(document)));
        WriteCommandFiles(folder, document.Commands);
        WriteMapFiles(folder, document.Maps);
    }

    public static List<object> NormalizeValues(IList<object>? values)
    {
        var normalized = new List<object>();
        if (values == null)
        {
            return normalized;
        }

        for (var i = 0; i < values.Count; i++)
        {
            normalized.Add(NormalizeValue(values[i]));
        }

        return normalized;
    }

    public static string RelativePath(string folder, string fullPath)
    {
        var prefix = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            fullPath.Length > prefix.Length + 1)
        {
            return fullPath.Substring(prefix.Length + 1);
        }

        return Path.GetFileName(fullPath);
    }

    private static object NormalizeValue(object? value)
    {
        if (value is bool)
        {
            return value;
        }

        if (value is string text)
        {
            bool flag;
            if (bool.TryParse(text, out flag))
            {
                return flag;
            }

            return Convert.ToInt32(text, CultureInfo.InvariantCulture);
        }

        if (value is IConvertible)
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        return value ?? 0;
    }

    private static void WriteCommandFiles(string folder, IList<CommandSpec> commands)
    {
        if (commands == null || commands.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(Path.Combine(folder, ProtocolPackLoader.CommandsDirectoryName));
        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            var relative = string.IsNullOrEmpty(command.SourcePath)
                ? Path.Combine(ProtocolPackLoader.CommandsDirectoryName, SanitizeFileName(command.Name) + ".kable.yaml")
                : command.SourcePath;
            WriteText(folder, relative, ProtocolPackYaml.SerializeDocument(command));
        }
    }

    private static void WriteMapFiles(string folder, IList<MapSpec> maps)
    {
        if (maps == null || maps.Count == 0)
        {
            return;
        }

        var groups = new List<MapFileGroup>();
        for (var i = 0; i < maps.Count; i++)
        {
            var map = maps[i];
            var relative = string.IsNullOrEmpty(map.SourcePath)
                ? Path.Combine(ProtocolPackLoader.MapsDirectoryName, SanitizeFileName(map.Name) + ".kable.yaml")
                : map.SourcePath;
            AddToGroup(groups, relative, map);
        }

        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            WriteText(folder, group.RelativePath, ProtocolPackYaml.SerializeDocuments(group.Maps));
        }
    }

    private static void AddToGroup(List<MapFileGroup> groups, string relativePath, MapSpec map)
    {
        for (var i = 0; i < groups.Count; i++)
        {
            if (string.Equals(groups[i].RelativePath, relativePath, StringComparison.OrdinalIgnoreCase))
            {
                groups[i].Maps.Add(map);
                return;
            }
        }

        var created = new MapFileGroup(relativePath);
        created.Maps.Add(map);
        groups.Add(created);
    }

    private static void WriteText(string folder, string relativePath, string contents)
    {
        var path = Path.Combine(folder, relativePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, contents);
    }

    private static ProtocolPackDocument ToRoot(ProtocolPackDocument document)
    {
        return new ProtocolPackDocument
        {
            ProtocolPackVersion = document.ProtocolPackVersion,
            Name = document.Name,
            Codec = document.Codec,
            Endpoint = document.Endpoint,
            UnitId = document.UnitId,
            Unmatched = document.Unmatched,
            OutOfRange = document.OutOfRange,
            Csharp = document.Csharp,
        };
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "unnamed";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            for (var j = 0; j < invalid.Length; j++)
            {
                if (chars[i] == invalid[j])
                {
                    chars[i] = '_';
                    break;
                }
            }
        }

        return new string(chars);
    }

    private sealed class MapFileGroup
    {
        public MapFileGroup(string relativePath)
        {
            RelativePath = relativePath;
            Maps = new List<MapSpec>();
        }

        public string RelativePath { get; }

        public List<MapSpec> Maps { get; }
    }
}
