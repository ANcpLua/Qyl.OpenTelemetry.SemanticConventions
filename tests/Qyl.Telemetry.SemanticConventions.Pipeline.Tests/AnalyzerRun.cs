using System.Collections.Immutable;
using System.Diagnostics;
using ANcpLua.Roslyn.Utilities.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;
/// <summary>
/// Runs one analyzer over a source that references the real convention packages and, optionally,
/// stub libraries compiled under a chosen assembly name (rules such as QYL0120 key on the
/// assembly a method comes from).
/// </summary>
internal static class AnalyzerRun
{
    public static CSharpCompilation CompileSource(Compile source)
    {
        var result = source.Build();
        Assert.IsTrue(result.Succeeded, "Analyzer fixture compilation failed:\n" + result.FormatDiagnostics());
        return result.Compilation;
    }

    public static async Task<ImmutableArray<Diagnostic>> DiagnosticsAsync(DiagnosticAnalyzer analyzer, string id, string source, IReadOnlyDictionary<string, string>? buildProperties = null, (string Name, string Source)[]? libraries = null, CancellationToken cancellationToken = default)
    {
        var references = new List<MetadataReference>();
        foreach (var(name, librarySource)in libraries ?? [])
        {
            var library = Compile.Source(librarySource).WithAssemblyName(name).Build().Compilation.AddReferences(references);
            Assert.IsEmpty(library.GetDiagnostics(cancellationToken).Where(static d => d.Severity == DiagnosticSeverity.Error), $"the stub library {name} must compile");
            references.Add(library.ToMetadataReference());
        }

        var compilation = Compile.Source(source).WithReference(typeof(global::Qyl.Spans).Assembly).WithReference(typeof(IncubatingSpans).Assembly).WithReference<Activity>().Build().Compilation.AddReferences(references);
        Assert.IsEmpty(compilation.GetDiagnostics(cancellationToken).Where(static d => d.Severity == DiagnosticSeverity.Error), "the analyzed source must compile");
        var diagnostics = await compilation.WithAnalyzers([analyzer], new AnalyzerOptions([], new DictionaryAnalyzerConfigOptionsProvider(buildProperties ?? new Dictionary<string, string>()))).GetAnalyzerDiagnosticsAsync(cancellationToken);
        Assert.IsFalse(diagnostics.Any(static diagnostic => diagnostic.Id.StartsWith("AD", StringComparison.Ordinal)),
            "Analyzer execution failed:\n" + string.Join("\n", diagnostics));
        return[..diagnostics.Where(d => d.Id == id)];
    }
}
