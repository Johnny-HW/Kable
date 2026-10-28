namespace Kable.Generators.ProtocolPack;

using System;
using Microsoft.CodeAnalysis;

internal static class ProtocolPackSyntaxProvider
{
    public static void Register(IncrementalGeneratorInitializationContext context)
    {
        var files = context.AdditionalTextsProvider
            .Where(static text => IsPackYaml(text.Path))
            .Select(static (text, cancellationToken) => AdditionalPackFile.Capture(text, cancellationToken));

        context.RegisterSourceOutput(files.Collect(), static (productionContext, captured) =>
        {
            try
            {
                ProtocolPackEmitter.Execute(productionContext, captured);
            }
            catch (Exception ex)
            {
                productionContext.ReportDiagnostic(ProtocolPackDiagnostics.Unexpected(ex.ToString()));
            }
        });
    }

    internal static bool IsPackYaml(string path) =>
        path.EndsWith(".kable.yaml", StringComparison.OrdinalIgnoreCase);
}
