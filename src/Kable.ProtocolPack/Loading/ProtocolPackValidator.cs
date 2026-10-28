using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Kable.ProtocolPack.Models;

namespace Kable.ProtocolPack.Loading;

/// <summary>
/// Schema rules from PROTOCOL_PACK.md. Collects every error; never applies a partial folder.
/// </summary>
public static class ProtocolPackValidator
{
    public const int SupportedVersion = 1;

    private static readonly Regex PlaceholderPattern = new Regex(
        @"\{([A-Za-z_][A-Za-z0-9]*)\}",
        RegexOptions.Compiled);

    public static IReadOnlyList<string> Validate(ProtocolPackDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var errors = new List<string>();
        ValidateVersion(document, errors);
        ValidateCodec(document, errors);
        ValidateCommands(document, errors);
        ValidateMaps(document, errors);
        return errors;
    }

    public static void EnsureValid(ProtocolPackDocument document)
    {
        var errors = Validate(document);
        if (errors.Count > 0)
        {
            throw new ProtocolPackException(errors);
        }
    }

    private static void ValidateVersion(ProtocolPackDocument document, List<string> errors)
    {
        if (document.ProtocolPackVersion != SupportedVersion)
        {
            errors.Add(
                "Unsupported protocol pack version " + document.ProtocolPackVersion +
                " (supported: " + SupportedVersion + ").");
        }
    }

    private static void ValidateCodec(ProtocolPackDocument document, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(document.Name))
        {
            errors.Add("Protocol pack name is required.");
        }

        if (!ProtocolPackCodecs.IsKnown(document.Codec))
        {
            errors.Add("Unknown or missing codec '" + (document.Codec ?? string.Empty) + "'.");
        }
    }

    private static void ValidateCommands(ProtocolPackDocument document, List<string> errors)
    {
        var matches = new Dictionary<string, string>(StringComparer.Ordinal);
        var commands = document.Commands ?? Array.Empty<CommandSpec>();
        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            ValidateCommandFileFields(command, errors);
            ValidatePlaceholders(command, errors);

            if (string.IsNullOrEmpty(command.Match))
            {
                continue;
            }

            if (matches.TryGetValue(command.Match, out var existing))
            {
                errors.Add(
                    "Duplicate match '" + command.Match + "' on commands '" +
                    existing + "' and '" + command.Name + "'.");
            }
            else
            {
                matches[command.Match] = command.Name;
            }
        }
    }

    private static void ValidateCommandFileFields(CommandSpec command, List<string> errors)
    {
        if (!string.IsNullOrEmpty(command.Codec))
        {
            errors.Add(
                "Command file '" + DisplayName(command) +
                "' must not declare codec (session codec lives on protocol.kable.yaml).");
        }

        if (command.Endpoint != null)
        {
            errors.Add(
                "Command file '" + DisplayName(command) +
                "' must not declare endpoint.");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            errors.Add("Command name is required (" + Source(command) + ").");
        }

        if (string.IsNullOrWhiteSpace(command.Match))
        {
            errors.Add("Command '" + DisplayName(command) + "' match is required.");
        }
    }

    private static void ValidatePlaceholders(CommandSpec command, List<string> errors)
    {
        if (string.IsNullOrEmpty(command.Match))
        {
            return;
        }

        var paramNames = new HashSet<string>(StringComparer.Ordinal);
        var parameters = command.Params ?? Array.Empty<ParamSpec>();
        for (var i = 0; i < parameters.Count; i++)
        {
            if (!string.IsNullOrEmpty(parameters[i].Name))
            {
                paramNames.Add(parameters[i].Name);
            }
        }

        foreach (Match match in PlaceholderPattern.Matches(command.Match))
        {
            var placeholder = match.Groups[1].Value;
            if (!paramNames.Contains(placeholder))
            {
                errors.Add(
                    "Command '" + DisplayName(command) + "' match placeholder '{" +
                    placeholder + "}' has no params entry.");
            }
        }
    }

    private static void ValidateMaps(ProtocolPackDocument document, List<string> errors)
    {
        var maps = document.Maps ?? Array.Empty<MapSpec>();
        var isModbus = ProtocolPackCodecs.IsModbus(document.Codec);
        var isMelsec = ProtocolPackCodecs.IsMelsec(document.Codec);
        for (var i = 0; i < maps.Count; i++)
        {
            var map = maps[i];
            if (string.IsNullOrWhiteSpace(map.Name))
            {
                errors.Add("Map name is required (" + Source(map) + ").");
            }

            if (map.HasScalarValue || map.Value != null)
            {
                errors.Add(
                    "Modbus map '" + DisplayName(map) +
                    "' must not use scalar 'value'; use addr+values[] or range.start/count.");
            }

            if (isMelsec && !MelsecDeviceCodeNames.IsKnown(map.Code))
            {
                errors.Add(
                    "MELSEC device code '" + (map.Code ?? string.Empty) +
                    "' is not a MelsecDeviceCode (map '" + DisplayName(map) + "').");
            }

            if (isModbus && map.Range == null && (map.Values == null || map.Values.Count == 0) && map.Value == null)
            {
                errors.Add(
                    "Modbus map '" + DisplayName(map) +
                    "' requires values[] or range.start/count.");
            }
        }
    }

    private static string DisplayName(CommandSpec command) =>
        string.IsNullOrEmpty(command.Name) ? Source(command) : command.Name;

    private static string DisplayName(MapSpec map) =>
        string.IsNullOrEmpty(map.Name) ? Source(map) : map.Name;

    private static string Source(CommandSpec command) =>
        string.IsNullOrEmpty(command.SourcePath) ? "command" : command.SourcePath;

    private static string Source(MapSpec map) =>
        string.IsNullOrEmpty(map.SourcePath) ? "map" : map.SourcePath;
}
