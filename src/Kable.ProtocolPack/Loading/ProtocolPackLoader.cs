using System;
using System.Collections.Generic;
using System.IO;
using Kable.ProtocolPack.Models;

namespace Kable.ProtocolPack.Loading;

/// <summary>
/// Loads and saves a protocol folder. Validation failure throws and applies nothing.
/// </summary>
public static class ProtocolPackLoader
{
    public const string ProtocolFileName = "protocol.kable.yaml";
    public const string CommandsDirectoryName = "commands";
    public const string MapsDirectoryName = "maps";
    public const string PackFilePattern = "*.kable.yaml";

    public static ProtocolPackDocument Load(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentNullException(nameof(folderPath));
        }

        var fullFolder = Path.GetFullPath(folderPath);
        if (!Directory.Exists(fullFolder))
        {
            throw new ProtocolPackException("Protocol folder not found: " + fullFolder);
        }

        var protocolPath = Path.Combine(fullFolder, ProtocolFileName);
        if (!File.Exists(protocolPath))
        {
            throw new ProtocolPackException(
                "Missing " + ProtocolFileName + " in folder '" + fullFolder + "'.");
        }

        var document = ReadProtocolFile(protocolPath);
        document.FolderPath = fullFolder;
        document.Commands = ReadCommandFiles(fullFolder);
        document.Maps = ReadMapFiles(fullFolder);
        ProtocolPackValidator.EnsureValid(document);
        return document;
    }

    public static void Save(string folderPath, ProtocolPackDocument document)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentNullException(nameof(folderPath));
        }

        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        ProtocolPackValidator.EnsureValid(document);
        ProtocolPackPersistence.Save(Path.GetFullPath(folderPath), document);
    }

    private static ProtocolPackDocument ReadProtocolFile(string protocolPath)
    {
        try
        {
            var yaml = File.ReadAllText(protocolPath);
            var document = ProtocolPackYaml.DeserializeDocument<ProtocolPackDocument>(yaml);
            if (document == null)
            {
                throw new ProtocolPackException("Empty protocol file: " + protocolPath);
            }

            return document;
        }
        catch (ProtocolPackException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProtocolPackException("Failed to read '" + protocolPath + "': " + ex.Message, ex);
        }
    }

    private static IList<CommandSpec> ReadCommandFiles(string folder)
    {
        var commandsDir = Path.Combine(folder, CommandsDirectoryName);
        if (!Directory.Exists(commandsDir))
        {
            return new List<CommandSpec>();
        }

        var files = Directory.GetFiles(commandsDir, PackFilePattern);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        var commands = new List<CommandSpec>(files.Length);
        for (var i = 0; i < files.Length; i++)
        {
            commands.Add(ReadCommandFile(folder, files[i]));
        }

        return commands;
    }

    private static CommandSpec ReadCommandFile(string folder, string path)
    {
        try
        {
            var yaml = File.ReadAllText(path);
            var command = ProtocolPackYaml.DeserializeDocument<CommandSpec>(yaml);
            if (command == null)
            {
                throw new ProtocolPackException("Empty command file: " + path);
            }

            command.SourcePath = ProtocolPackPersistence.RelativePath(folder, path);
            command.Params = command.Params ?? new List<ParamSpec>();
            command.Errors = command.Errors ?? new List<ErrorOutcomeSpec>();
            return command;
        }
        catch (ProtocolPackException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProtocolPackException("Failed to read '" + path + "': " + ex.Message, ex);
        }
    }

    private static IList<MapSpec> ReadMapFiles(string folder)
    {
        var mapsDir = Path.Combine(folder, MapsDirectoryName);
        if (!Directory.Exists(mapsDir))
        {
            return new List<MapSpec>();
        }

        var files = Directory.GetFiles(mapsDir, PackFilePattern);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        var maps = new List<MapSpec>();
        for (var i = 0; i < files.Length; i++)
        {
            ReadMapFile(folder, files[i], maps);
        }

        return maps;
    }

    private static void ReadMapFile(string folder, string path, IList<MapSpec> maps)
    {
        try
        {
            var yaml = File.ReadAllText(path);
            var documents = ProtocolPackYaml.DeserializeMapDocuments(yaml);
            for (var d = 0; d < documents.Count; d++)
            {
                var map = documents[d];
                map.SourcePath = ProtocolPackPersistence.RelativePath(folder, path);
                map.Values = ProtocolPackPersistence.NormalizeValues(map.Values);
                maps.Add(map);
            }
        }
        catch (ProtocolPackException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProtocolPackException("Failed to read '" + path + "': " + ex.Message, ex);
        }
    }
}
