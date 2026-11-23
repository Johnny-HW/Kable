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
            var trimmed = text.Trim();
            if (bool.TryParse(trimmed, out var flag))
            {
                return flag;
            }

            if (trimmed.Length > 2 &&
                trimmed[0] == '0' &&
                (trimmed[1] == 'x' || trimmed[1] == 'X'))
            {
                if (long.TryParse(trimmed.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexSigned))
                {
                    return hexSigned >= int.MinValue && hexSigned <= int.MaxValue ? (object)(int)hexSigned : hexSigned;
                }

                if (ulong.TryParse(trimmed.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexUnsigned))
                {
                    return hexUnsigned;
                }
            }

            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i32))
            {
                return i32;
            }

            if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i64))
            {
                return i64;
            }

            if (double.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var dbl) &&
                !double.IsNaN(dbl) && !double.IsInfinity(dbl))
            {
                return dbl;
            }

            return text;
        }

        if (value is int || value is long || value is ulong || value is double || value is float || value is decimal)
        {
            if (value is long l64 && l64 >= int.MinValue && l64 <= int.MaxValue)
            {
                return (int)l64;
            }

            return value;
        }

        if (value is byte || value is sbyte || value is short || value is ushort || value is uint)
        {
            var asLong = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            return asLong >= int.MinValue && asLong <= int.MaxValue ? (object)(int)asLong : asLong;
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
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new ProtocolPackException("Refusing to write to an invalid or rooted protocol pack path: '" + (relativePath ?? string.Empty) + "'.");
        }

        var rootFull = Path.GetFullPath(folder);
        var rootPrefix = rootFull.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                         rootFull.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? rootFull
            : rootFull + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(rootFull, relativePath));
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ProtocolPackException("Refusing to write outside protocol pack folder: '" + relativePath + "'.");
        }

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
