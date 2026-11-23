namespace Kable.Generators.Tests;

using System.Linq;
using System.Reflection;
using FluentAssertions;
using Kable.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class ProtocolSourceGeneratorRegressionTests
{
    private static MetadataReference[] BaseReferences =>
    [
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
    ];

    [Fact]
    public void IncrementalDriver_EditingTemplateOrUrgent_RegeneratesUpdatedCode()
    {
        const string initial = @"
namespace IncApp;
using Kable.Generators;

[DeviceCommand(""CMD_A.{Value}"")]
public readonly partial record struct SampleCommand(int Value);

[DeviceCommand(""UNRELATED"")]
public readonly partial record struct UnrelatedCommand;
";
        const string updated = @"
namespace IncApp;
using Kable.Generators;

[DeviceCommand(""CMD_B.{Value}"", IsUrgent = true)]
public readonly partial record struct SampleCommand(int Value);

[DeviceCommand(""UNRELATED"")]
public readonly partial record struct UnrelatedCommand;
";
        var tree1 = CSharpSyntaxTree.ParseText(initial, path: "Commands.cs");
        var compilation1 = CSharpCompilation.Create("IncTest", [tree1], BaseReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ProtocolSourceGenerator().AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation1);
        var firstRun = driver.GetRunResult();
        firstRun.GeneratedTrees.Select(t => t.ToString())
            .First(s => s.Contains("SampleCommand"))
            .Should().Contain("CMD_A.{Value}").And.Contain("IsUrgent => false;");

        var tree2 = tree1.WithChangedText(Microsoft.CodeAnalysis.Text.SourceText.From(updated));
        var compilation2 = compilation1.ReplaceSyntaxTree(tree1, tree2);

        driver = driver.RunGenerators(compilation2);
        var secondRun = driver.GetRunResult();
        secondRun.Diagnostics.Should().BeEmpty();
        var updatedCode = secondRun.GeneratedTrees.Select(t => t.ToString())
            .First(s => s.Contains("SampleCommand"));
        updatedCode.Should().Contain("CMD_B.{Value}").And.Contain("IsUrgent => true;");
    }

    [Fact]
    public void Generator_SameTypeNameAcrossNamespacesNestedAndGeneric_CompilesWithoutCollision()
    {
        const string source = @"
using Kable.Generators;

namespace Alpha
{
    [DeviceCommand(""ALPHA"")]
    public readonly partial record struct PingCommand;
}

namespace Beta
{
    [DeviceCommand(""BETA"")]
    public readonly partial record struct PingCommand;

    public partial class Container<TOuter>
    {
        [DeviceCommand(""NESTED.{Payload}"")]
        public readonly partial record struct NestedCommand<TInner>(TInner Payload);
    }
}
";
        var compilation = CSharpCompilation.Create("CollisionTest",
            [CSharpSyntaxTree.ParseText(source)],
            BaseReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ProtocolSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var runResult = driver.GetRunResult();
        runResult.GeneratedTrees.Select(t => t.FilePath).Should().Contain(p => p.EndsWith("Alpha.PingCommand.g.cs"));
        runResult.GeneratedTrees.Select(t => t.FilePath).Should().Contain(p => p.EndsWith("Beta.PingCommand.g.cs"));
        runResult.GeneratedTrees.Select(t => t.FilePath).Should().Contain(p => p.Contains("Beta.Container_TOuter_.NestedCommand_1.g.cs"));
    }

    [Fact]
    public void Generator_NonPartialType_ReportsKableGen001Diagnostic()
    {
        const string source = @"
namespace DiagApp;
using Kable.Generators;

[DeviceCommand(""BAD"")]
public sealed class NotPartialCommand;
";
        var compilation = CSharpCompilation.Create("DiagTest",
            [CSharpSyntaxTree.ParseText(source)],
            BaseReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ProtocolSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        diagnostics.Should().ContainSingle(d => d.Id == "KABLEGEN001" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Generator_TemplateWithQuotesBackslashNewlineAndForeignAttribute_HandledSafely()
    {
        const string source = @"
namespace EscapeApp
{
    using System;

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DeviceCommandAttribute : Attribute
    {
        public DeviceCommandAttribute(string t) { }
    }

    [DeviceCommand(""IGNORED"")]
    public sealed class ForeignAttributedType { }
}

namespace RealApp
{
    using Kable.Generators;

    [DeviceCommand(""MSG \""OK\""\\{Val}\n"")]
    public readonly partial record struct EscapedCommand(double Val);
}
";
        var compilation = CSharpCompilation.Create("EscapeTest",
            [CSharpSyntaxTree.ParseText(source)],
            BaseReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ProtocolSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        driver.GetRunResult().GeneratedTrees.Should().NotContain(t => t.ToString().Contains("ForeignAttributedType"));
        driver.GetRunResult().GeneratedTrees.Select(t => t.ToString())
            .First(s => s.Contains("EscapedCommand"))
            .Should().Contain("FormattableString.Invariant");
    }
}
