namespace Kable.Generators.ProtocolPack;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Kable.ProtocolPack.Loading;
using Kable.ProtocolPack.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

internal static class ProtocolPackEmitter
{
    public static void Execute(SourceProductionContext context, ImmutableArray<AdditionalPackFile> files)
    {
        if (files.IsDefaultOrEmpty)
        {
            return;
        }

        try
        {
            var packs = ProtocolPackFileBinder.Bind(files, out var orphans);
            for (var i = 0; i < orphans.Count; i++)
            {
                context.ReportDiagnostic(ProtocolPackDiagnostics.Orphan(orphans[i].Path));
            }

            for (var i = 0; i < packs.Count; i++)
            {
                EmitPack(context, packs[i]);
            }
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(ProtocolPackDiagnostics.Unexpected(ex.Message));
        }
    }

    private static void EmitPack(SourceProductionContext context, BoundProtocolPack bound)
    {
        if (!TryReadDocument(context, bound, out var document) || document == null)
        {
            return;
        }

        var errors = ProtocolPackValidator.Validate(document);
        if (errors.Count > 0)
        {
            for (var i = 0; i < errors.Count; i++)
            {
                context.ReportDiagnostic(ProtocolPackDiagnostics.Validation(bound.ProtocolFile.Path, errors[i]));
            }

            return;
        }

        var ns = ResolveNamespace(context, bound, document);
        var typeKind = ProtocolPackIdentifiers.EnsurePartial(
            string.IsNullOrWhiteSpace(document.Csharp?.TypeKind)
                ? "readonly partial record struct"
                : document.Csharp!.TypeKind!);
        var packTypeName = ProtocolPackIdentifiers.SanitizeTypeName(document.Name);

        if (ProtocolPackCodecs.IsAsciiLine(document.Codec))
        {
            EmitCommands(context, ns, typeKind, document.Commands);
        }

        context.AddSource(
            ProtocolPackCSharp.HintName(ns, packTypeName + "Protocol"),
            SourceText.From(OptionsFactoryEmitter.Emit(ns, packTypeName, document), Encoding.UTF8));

        if ((ProtocolPackCodecs.IsModbus(document.Codec) || ProtocolPackCodecs.IsMelsec(document.Codec)) &&
            document.Maps != null &&
            document.Maps.Count > 0)
        {
            context.AddSource(
                ProtocolPackCSharp.HintName(ns, packTypeName + "Maps"),
                SourceText.From(MapConstantsEmitter.Emit(ns, packTypeName, document.Maps), Encoding.UTF8));
        }
    }

    private static bool TryReadDocument(
        SourceProductionContext context,
        BoundProtocolPack bound,
        out ProtocolPackDocument? document)
    {
        document = null;
        try
        {
            document = ProtocolPackYaml.DeserializeDocument<ProtocolPackDocument>(bound.ProtocolFile.Text);
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(ProtocolPackDiagnostics.Parse(bound.ProtocolFile.Path, ex.Message));
            return false;
        }

        if (document == null)
        {
            context.ReportDiagnostic(ProtocolPackDiagnostics.Parse(bound.ProtocolFile.Path, "Empty protocol file."));
            return false;
        }

        document.FolderPath = bound.RootPath;
        return TryReadCommands(context, bound, document) && TryReadMaps(context, bound, document);
    }

    private static bool TryReadCommands(
        SourceProductionContext context,
        BoundProtocolPack bound,
        ProtocolPackDocument document)
    {
        var commands = new List<CommandSpec>(bound.Commands.Count);
        for (var i = 0; i < bound.Commands.Count; i++)
        {
            var file = bound.Commands[i];
            try
            {
                var command = ProtocolPackYaml.DeserializeDocument<CommandSpec>(file.Text);
                if (command == null)
                {
                    context.ReportDiagnostic(ProtocolPackDiagnostics.Parse(file.Path, "Empty command file."));
                    return false;
                }

                command.SourcePath = file.Path;
                command.Params = command.Params ?? new List<ParamSpec>();
                command.Errors = command.Errors ?? new List<ErrorOutcomeSpec>();
                commands.Add(command);
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(ProtocolPackDiagnostics.Parse(file.Path, ex.Message));
                return false;
            }
        }

        document.Commands = commands;
        return true;
    }

    private static bool TryReadMaps(
        SourceProductionContext context,
        BoundProtocolPack bound,
        ProtocolPackDocument document)
    {
        var maps = new List<MapSpec>();
        for (var i = 0; i < bound.Maps.Count; i++)
        {
            var file = bound.Maps[i];
            try
            {
                var documents = ProtocolPackYaml.DeserializeMapDocuments(file.Text);
                for (var d = 0; d < documents.Count; d++)
                {
                    var map = documents[d];
                    map.SourcePath = file.Path;
                    map.Values = map.Values ?? new List<object>();
                    maps.Add(map);
                }
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(ProtocolPackDiagnostics.Parse(file.Path, ex.Message));
                return false;
            }
        }

        document.Maps = maps;
        return true;
    }

    private static string ResolveNamespace(
        SourceProductionContext context,
        BoundProtocolPack bound,
        ProtocolPackDocument document)
    {
        if (!string.IsNullOrWhiteSpace(document.Csharp?.Namespace))
        {
            return document.Csharp!.Namespace!.Trim();
        }

        var fallback = "Kable.Generated." + ProtocolPackIdentifiers.SanitizeTypeName(document.Name);
        context.ReportDiagnostic(ProtocolPackDiagnostics.MissingCsharpNamespace(bound.ProtocolFile.Path, fallback));
        return fallback;
    }

    private static void EmitCommands(
        SourceProductionContext context,
        string ns,
        string typeKind,
        IList<CommandSpec> commands)
    {
        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            context.AddSource(
                ProtocolPackCSharp.HintName(ns, command.Name + "Command"),
                SourceText.From(CommandTypeEmitter.Emit(ns, typeKind, command), Encoding.UTF8));
            context.AddSource(
                ProtocolPackCSharp.HintName(ns, command.Name + "Reply"),
                SourceText.From(ReplyEnumEmitter.Emit(ns, command), Encoding.UTF8));
        }
    }
}
