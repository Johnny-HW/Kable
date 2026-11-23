using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Kable.ProtocolPack.Models;

namespace Kable.ProtocolPack.Loading;

/// <summary>
/// Schema rules from PROTOCOL_PACK.md. Collects every error; never applies a partial folder.
/// </summary>
public static partial class ProtocolPackValidator
{
    public const int SupportedVersion = 1;

    private static readonly Regex PlaceholderPattern = new Regex(
        @"\{([A-Za-z_][A-Za-z0-9_]*)\}",
        RegexOptions.Compiled);

    private static readonly Regex IdentifierSegmentPattern = new Regex(
        @"^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.Compiled);

    private static readonly HashSet<string> AllowedTypeKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "record struct",
        "readonly record struct",
        "partial record struct",
        "readonly partial record struct",
        "record",
        "partial record",
        "record class",
        "partial record class",
        "class",
        "partial class",
        "sealed class",
        "sealed partial class",
        "struct",
        "readonly struct",
        "partial struct",
        "readonly partial struct",
    };

    private static readonly HashSet<string> AllowedParamTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "string",
        "int",
        "double",
    };

    public static IReadOnlyList<string> Validate(ProtocolPackDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var errors = new List<string>();
        ValidateVersion(document, errors);
        ValidateCodec(document, errors);
        ValidateEndpoint(document.Endpoint, errors);
        ValidateCsharp(document.Csharp, errors);
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
                "Unsupported protocol pack version " + document.ProtocolPackVersion.ToString(CultureInfo.InvariantCulture) +
                " (supported: " + SupportedVersion.ToString(CultureInfo.InvariantCulture) + ").");
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

    private static void ValidateEndpoint(EndpointSpec? endpoint, List<string> errors)
    {
        if (endpoint == null)
        {
            return;
        }

        if (endpoint.Port.HasValue && (endpoint.Port.Value < 1 || endpoint.Port.Value > 65535))
        {
            errors.Add("Endpoint port " + endpoint.Port.Value.ToString(CultureInfo.InvariantCulture) + " must be between 1 and 65535.");
        }

        if (endpoint.BaudRate.HasValue && endpoint.BaudRate.Value <= 0)
        {
            errors.Add("Endpoint baudRate " + endpoint.BaudRate.Value.ToString(CultureInfo.InvariantCulture) + " must be positive.");
        }
    }

    private static void ValidateCsharp(CsharpSpec? csharp, List<string> errors)
    {
        if (csharp == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(csharp.Namespace))
        {
            var segments = csharp.Namespace!.Trim().Split('.');
            for (var i = 0; i < segments.Length; i++)
            {
                if (!IdentifierSegmentPattern.IsMatch(segments[i]))
                {
                    errors.Add("Invalid csharp.namespace '" + csharp.Namespace + "'.");
                    break;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(csharp.TypeKind))
        {
            var normalized = NormalizeTypeKind(csharp.TypeKind!);
            if (!AllowedTypeKinds.Contains(normalized))
            {
                errors.Add("Unsupported csharp.typeKind '" + csharp.TypeKind + "'.");
            }
        }
    }

    private static string NormalizeTypeKind(string typeKind)
    {
        var trimmed = typeKind.Trim();
        if (trimmed.StartsWith("public ", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring("public ".Length).Trim();
        }

        return Regex.Replace(trimmed, @"\s+", " ");
    }
}
