namespace Kable.Generators.ProtocolPack;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Kable.ProtocolPack.Loading;

internal sealed class BoundProtocolPack
{
    public BoundProtocolPack(
        string rootPath,
        AdditionalPackFile protocolFile,
        IReadOnlyList<AdditionalPackFile> commands,
        IReadOnlyList<AdditionalPackFile> maps)
    {
        RootPath = rootPath;
        ProtocolFile = protocolFile;
        Commands = commands;
        Maps = maps;
    }

    public string RootPath { get; }

    public AdditionalPackFile ProtocolFile { get; }

    public IReadOnlyList<AdditionalPackFile> Commands { get; }

    public IReadOnlyList<AdditionalPackFile> Maps { get; }
}

internal static class ProtocolPackFileBinder
{
    public static IReadOnlyList<BoundProtocolPack> Bind(
        ImmutableArray<AdditionalPackFile> files,
        out IReadOnlyList<AdditionalPackFile> orphans)
    {
        var normalized = new AdditionalPackFile[files.Length];
        for (var i = 0; i < files.Length; i++)
        {
            normalized[i] = new AdditionalPackFile(Normalize(files[i].Path), files[i].Text);
        }

        var assigned = new bool[normalized.Length];
        var packs = new List<BoundProtocolPack>();
        for (var i = 0; i < normalized.Length; i++)
        {
            if (!IsProtocolFile(normalized[i].Path))
            {
                continue;
            }

            assigned[i] = true;
            packs.Add(BindPack(normalized, assigned, i));
        }

        var leftover = new List<AdditionalPackFile>();
        for (var i = 0; i < normalized.Length; i++)
        {
            if (!assigned[i])
            {
                leftover.Add(normalized[i]);
            }
        }

        orphans = leftover;
        return packs;
    }

    private static BoundProtocolPack BindPack(AdditionalPackFile[] files, bool[] assigned, int protocolIndex)
    {
        var protocol = files[protocolIndex];
        var root = ProtocolRoot(protocol.Path);
        var commands = new List<AdditionalPackFile>();
        var maps = new List<AdditionalPackFile>();
        var commandsPrefix = root + "/" + ProtocolPackLoader.CommandsDirectoryName + "/";
        var mapsPrefix = root + "/" + ProtocolPackLoader.MapsDirectoryName + "/";

        for (var i = 0; i < files.Length; i++)
        {
            if (assigned[i])
            {
                continue;
            }

            var path = files[i].Path;
            if (StartsWithDirectory(path, commandsPrefix))
            {
                commands.Add(files[i]);
                assigned[i] = true;
            }
            else if (StartsWithDirectory(path, mapsPrefix))
            {
                maps.Add(files[i]);
                assigned[i] = true;
            }
        }

        return new BoundProtocolPack(root, protocol, commands, maps);
    }

    private static string Normalize(string path) => (path ?? string.Empty).Replace('\\', '/');

    private static bool IsProtocolFile(string path)
    {
        var fileName = ProtocolPackLoader.ProtocolFileName;
        return path.EndsWith("/" + fileName, StringComparison.OrdinalIgnoreCase) ||
               path.Equals(fileName, StringComparison.OrdinalIgnoreCase);
    }

    private static string ProtocolRoot(string protocolPath)
    {
        var fileName = ProtocolPackLoader.ProtocolFileName;
        if (protocolPath.Length <= fileName.Length)
        {
            return string.Empty;
        }

        return protocolPath.Substring(0, protocolPath.Length - fileName.Length - 1);
    }

    private static bool StartsWithDirectory(string path, string prefix) =>
        path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
