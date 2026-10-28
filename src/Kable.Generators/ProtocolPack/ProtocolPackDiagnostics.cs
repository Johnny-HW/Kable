namespace Kable.Generators.ProtocolPack;

using Microsoft.CodeAnalysis;

internal static class ProtocolPackDiagnostics
{
    public static readonly DiagnosticDescriptor ParseFailed = new DiagnosticDescriptor(
        "KABLEPACK001",
        "Protocol pack YAML could not be parsed",
        "Failed to parse protocol pack YAML '{0}': {1}",
        "Kable.ProtocolPack",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ValidationFailed = new DiagnosticDescriptor(
        "KABLEPACK002",
        "Protocol pack validation failed",
        "Protocol pack '{0}' is invalid: {1}",
        "Kable.ProtocolPack",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingNamespace = new DiagnosticDescriptor(
        "KABLEPACK003",
        "Protocol pack csharp.namespace is missing",
        "Protocol pack '{0}' has no csharp.namespace; using '{1}'",
        "Kable.ProtocolPack",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor OrphanFile = new DiagnosticDescriptor(
        "KABLEPACK004",
        "Protocol pack YAML is not part of a protocol folder",
        "Protocol pack YAML '{0}' is not under a folder that contains protocol.kable.yaml and was ignored",
        "Kable.ProtocolPack",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnexpectedFailure = new DiagnosticDescriptor(
        "KABLEPACK005",
        "Protocol pack generator failed",
        "Protocol pack generator failed: {0}",
        "Kable.ProtocolPack",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static Diagnostic Parse(string path, string message) =>
        Diagnostic.Create(ParseFailed, Location.None, path, message);

    public static Diagnostic Validation(string path, string message) =>
        Diagnostic.Create(ValidationFailed, Location.None, path, message);

    public static Diagnostic MissingCsharpNamespace(string path, string fallbackNamespace) =>
        Diagnostic.Create(MissingNamespace, Location.None, path, fallbackNamespace);

    public static Diagnostic Orphan(string path) =>
        Diagnostic.Create(OrphanFile, Location.None, path);

    public static Diagnostic Unexpected(string message) =>
        Diagnostic.Create(UnexpectedFailure, Location.None, message);
}
