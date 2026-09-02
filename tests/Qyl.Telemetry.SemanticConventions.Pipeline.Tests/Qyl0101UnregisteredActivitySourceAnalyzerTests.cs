// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
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
        var diagnostics = await AnalyzerHarness.RunAsync(
            [new Qyl0101UnregisteredActivitySourceAnalyzer()],
            source,
            options: new AnalyzerOptions(
                [],
                new GlobalOptionsProvider(buildProperties ?? new Dictionary<string, string>())),
            excludeTestFrameworkReferences: true);

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

    private sealed class GlobalOptionsProvider(IReadOnlyDictionary<string, string> global)
        : AnalyzerConfigOptionsProvider
    {
        private readonly Options _options = new(global);
        public override AnalyzerConfigOptions GlobalOptions => _options;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;

        private sealed class Options(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value)
            {
                if (values.TryGetValue(key, out var v))
                {
                    value = v;
                    return true;
                }

                value = null!;
                return false;
            }
        }
    }
}
