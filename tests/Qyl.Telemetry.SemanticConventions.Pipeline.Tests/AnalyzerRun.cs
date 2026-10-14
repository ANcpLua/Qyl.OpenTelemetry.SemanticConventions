using System.Collections.Immutable;
using System.Diagnostics;
using ANcpLua.Roslyn.Utilities.Testing;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
/// Runs one analyzer over a source that references the real convention packages and, optionally,
/// stub libraries compiled under a chosen assembly name (rules such as QYL0120 key on the
/// assembly a method comes from).
/// </summary>
internal static class AnalyzerRun
{
    public static async Task<ImmutableArray<Diagnostic>> DiagnosticsAsync(
        DiagnosticAnalyzer analyzer,
        string id,
        string source,
        IReadOnlyDictionary<string, string>? buildProperties = null,
        params (string Name, string Source)[] libraries)
    {
        var references = new List<MetadataReference>();
        foreach (var (name, librarySource) in libraries)
        {
            var library = Compile.Source(librarySource).WithAssemblyName(name).Build().Compilation.AddReferences(references);
            library.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error)
                .Should().BeEmpty($"the stub library {name} must compile");
            references.Add(library.ToMetadataReference());
        }

        var compilation = Compile
            .Source(source)
            .WithReference(typeof(global::Qyl.Spans).Assembly)
            .WithReference(typeof(global::Qyl.IncubatingSpans).Assembly)
            .WithReference<Activity>()
            .Build()
            .Compilation
            .AddReferences(references);

        compilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("the analyzed source must compile");

        var diagnostics = await compilation
            .WithAnalyzers(
                [analyzer],
                new AnalyzerOptions([], new DictionaryAnalyzerConfigOptionsProvider(buildProperties ?? new Dictionary<string, string>())))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        return [.. diagnostics.Where(d => d.Id == id)];
    }
}
