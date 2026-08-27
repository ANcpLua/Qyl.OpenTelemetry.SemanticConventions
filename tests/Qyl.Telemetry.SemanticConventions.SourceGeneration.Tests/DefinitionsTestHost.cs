using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Tests;

/// <summary>
/// Runs a generator against the trusted-platform reference set plus, by default, the
/// real <c>Qyl.Telemetry.SemanticConventions</c> vocabulary assembly. The definition
/// surfaces emit fields typed against that package, so compiling the generated output
/// here proves the emitted text binds to the shipped types (including the
/// <c>default(TInstrument).Kind</c> marker design) rather than to a per-consumer copy.
/// </summary>
internal static class DefinitionsTestHost
{
    private static readonly MetadataReference[] s_platformReferences =
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("Trusted platform assemblies are unavailable."))
        .Split(Path.PathSeparator)
        // The test process itself loads the vocabulary assembly (and this repository's other
        // outputs); keep them out of the platform set so referenceVocabulary is the only way in.
        .Where(static path => !Path.GetFileName(path).StartsWith("Qyl.", StringComparison.Ordinal))
        .Select(static path => MetadataReference.CreateFromFile(path))
        .ToArray();

    private static readonly MetadataReference s_vocabularyReference =
        MetadataReference.CreateFromFile(typeof(MetricDefinition<>).Assembly.Location);

    public static (GeneratorDriverRunResult RunResult, Compilation OutputCompilation) Run<TGenerator>(
        string source,
        bool referenceVocabulary = true)
        where TGenerator : IIncrementalGenerator, new()
    {
        var references = referenceVocabulary
            ? [.. s_platformReferences, s_vocabularyReference]
            : s_platformReferences;

        var compilation = CSharpCompilation.Create(
            "DefinitionsTestAssembly",
            [CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new TGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out _,
            TestContext.Current.CancellationToken);

        return (driver.GetRunResult(), outputCompilation);
    }

    public static string GeneratedText(this GeneratorDriverRunResult result, string fileSuffix) =>
        result.GeneratedTrees
            .Single(t => t.FilePath.EndsWith(fileSuffix, StringComparison.Ordinal))
            .ToString();

    public static ImmutableArray<Diagnostic> Errors(this Compilation compilation) =>
        [.. compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error)];
}
