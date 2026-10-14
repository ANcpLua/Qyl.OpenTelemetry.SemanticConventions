// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ANcpLua.Roslyn.Utilities.Testing;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.Telemetry.SemanticConventions.Analyzers;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
///   QYL0101 fires on an ActivitySource that no AddSource() call in the compilation registers,
///   and stays silent when the project opts out as an instrumentation library, because such a
///   library's sources are registered by a separate hosting package.
/// </summary>
public sealed class Qyl0101UnregisteredActivitySourceAnalyzerTests
{
    private const string Library = """
        using System.Diagnostics;

        public static class Instrumentation
        {
            public static readonly ActivitySource Source = new("Contoso.Instrumentation");
        }
        """;

    private static async Task<ImmutableArray<Diagnostic>> RunAsync(
        string source,
        IReadOnlyDictionary<string, string>? buildProperties = null)
    {
        var diagnostics = await Compile
            .Source(source)
            .WithReference<ActivitySource>()
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers(
                [new Qyl0101UnregisteredActivitySourceAnalyzer()],
                new AnalyzerOptions(
                    [],
                    new DictionaryAnalyzerConfigOptionsProvider(buildProperties ?? new Dictionary<string, string>())))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        return [.. diagnostics.Where(d => d.Id == "QYL0101")];
    }

    [Fact]
    public async Task Fires_on_an_activity_source_nothing_registers()
    {
        var diagnostics = await RunAsync(Library);

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("Contoso.Instrumentation");
    }

    [Fact]
    public async Task Silent_when_the_project_opts_out_as_an_instrumentation_library()
    {
        var diagnostics = await RunAsync(
            Library,
            new Dictionary<string, string>
            {
                ["build_property.OtelSemConvInstrumentationLibrary"] = "true",
            });

        diagnostics.Should().BeEmpty();
    }
}
