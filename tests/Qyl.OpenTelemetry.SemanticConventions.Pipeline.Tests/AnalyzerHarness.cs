// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Qyl.OpenTelemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
/// Shared analyzer test harness: compiles a consumer source against the full
/// trusted-platform reference set, asserts it compiles, and runs the analyzers.
/// </summary>
internal static class AnalyzerHarness
{
    public static readonly MetadataReference[] PlatformReferences =
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("Trusted platform assemblies are unavailable."))
        .Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path))
        .ToArray();

    public static Task<ImmutableArray<Diagnostic>> RunAsync(
        DiagnosticAnalyzer analyzer,
        string source,
        string filePath = "/repo/Consumer.cs") =>
        RunAsync([analyzer], source, filePath);

    public static async Task<ImmutableArray<Diagnostic>> RunAsync(
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        string source,
        string filePath = "/repo/Consumer.cs",
        ImmutableArray<MetadataReference> additionalReferences = default,
        AnalyzerOptions? options = null)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            filePath);
        var compilation = CSharpCompilation.Create(
            $"analyzer-harness-{Guid.NewGuid():N}",
            [syntaxTree],
            additionalReferences.IsDefaultOrEmpty
                ? PlatformReferences
                : [.. PlatformReferences, .. additionalReferences],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        errors.Should().BeEmpty(
            "the analyzer test source must compile: {0}",
            string.Join(Environment.NewLine, errors.Select(error => error.ToString())));

        var withAnalyzers = options is null
            ? compilation.WithAnalyzers(analyzers)
            : compilation.WithAnalyzers(analyzers, options);
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    /// <summary>Compiles a fixture source and returns it as a metadata reference.</summary>
    public static PortableExecutableReference CompileReference(string source)
    {
        var compilation = CSharpCompilation.Create(
            $"fixture-{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest))],
            PlatformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        emit.Success.Should().BeTrue(
            "the referenced fixture must compile: {0}",
            string.Join(Environment.NewLine, emit.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }
}
