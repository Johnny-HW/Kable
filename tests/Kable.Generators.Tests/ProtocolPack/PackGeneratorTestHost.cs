namespace Kable.Generators.Tests.ProtocolPack;

using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Kable.Configuration;
using Kable.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class PackGeneratorTestHost
{
    public static string MassHunterFolder =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "MassHunter-7700");

    public static ImmutableArray<AdditionalText> LoadMassHunter() => LoadFolder(MassHunterFolder);

    public static ImmutableArray<AdditionalText> LoadFolder(string folder)
    {
        var files = Directory.GetFiles(folder, "*.kable.yaml", SearchOption.AllDirectories);
        var builder = ImmutableArray.CreateBuilder<AdditionalText>(files.Length);
        for (var i = 0; i < files.Length; i++)
        {
            builder.Add(new InMemoryAdditionalText(files[i], File.ReadAllText(files[i])));
        }

        return builder.ToImmutable();
    }

    public static GeneratorDriverRunResult Run(params AdditionalText[] files) =>
        Run(files.ToImmutableArray());

    public static GeneratorDriverRunResult Run(ImmutableArray<AdditionalText> files)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var compilation = CSharpCompilation.Create(
            "PackGenInput",
            new[] { CSharpSyntaxTree.ParseText("namespace PackGenInput { public static class Anchor { } }", parseOptions) },
            MetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var additionalTexts = files.IsDefault ? ImmutableArray<AdditionalText>.Empty : files;
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new ISourceGenerator[] { new ProtocolSourceGenerator().AsSourceGenerator() },
            additionalTexts: additionalTexts,
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        try
        {
            return driver.GetRunResult();
        }
        catch (NullReferenceException ex)
        {
            var detail = string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString()));
            throw new InvalidOperationException(
                "Generator GetRunResult failed. Diagnostics:" + Environment.NewLine + detail,
                ex);
        }
    }

    public static Assembly Emit(GeneratorDriverRunResult runResult)
    {
        var compilation = CSharpCompilation.Create(
            "PackGenOutput_" + Guid.NewGuid().ToString("N"),
            runResult.GeneratedTrees,
            MetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        emitted.Success.Should().BeTrue(
            string.Join(Environment.NewLine, emitted.Diagnostics.Select(d => d.ToString())));

        return Assembly.Load(stream.ToArray());
    }

    public static IEnumerable<string> GeneratedSources(GeneratorDriverRunResult runResult) =>
        runResult.GeneratedTrees.Select(tree => tree.ToString());

    private static ImmutableArray<MetadataReference> MetadataReferences()
    {
        var locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var references = ImmutableArray.CreateBuilder<MetadataReference>();

        void Add(Assembly assembly)
        {
            if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location))
            {
                return;
            }

            if (!locations.Add(assembly.Location))
            {
                return;
            }

            references.Add(MetadataReference.CreateFromFile(assembly.Location));
        }

        Add(typeof(object).Assembly);
        Add(typeof(Enumerable).Assembly);
        Add(typeof(KableDeviceOptions).Assembly);
        Add(Assembly.Load("System.Runtime"));

        var loaded = AppDomain.CurrentDomain.GetAssemblies();
        for (var i = 0; i < loaded.Length; i++)
        {
            Add(loaded[i]);
        }

        return references.ToImmutable();
    }
}
