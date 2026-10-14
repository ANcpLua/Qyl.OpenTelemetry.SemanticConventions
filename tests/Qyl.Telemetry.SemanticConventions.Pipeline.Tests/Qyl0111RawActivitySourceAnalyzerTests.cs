using System.Globalization;
using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.Analyzers;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>QYL0111: application code starts spans only through the generated starters (ADR D2).</summary>
public sealed class Qyl0111RawActivitySourceAnalyzerTests
{
    private const string RawSource = """
        using System.Diagnostics;

        public static class Store
        {
            private static readonly ActivitySource Source = new("Shop.Store");

            public static void Write()
            {
                using var span = Source.StartActivity("write orders");
            }
        }
        """;

    [Fact]
    public async Task Reports_a_new_ActivitySource_and_a_raw_StartActivity()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(new Qyl0111RawActivitySourceAnalyzer(), "QYL0111", RawSource);

        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(static d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        diagnostics.Select(static d => d.GetMessage(CultureInfo.InvariantCulture))
            .Should().Contain(static m => m.Contains("ActivitySource", StringComparison.Ordinal))
            .And.Contain(static m => m.Contains("StartActivity", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Is_silent_in_an_instrumentation_library()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(
            new Qyl0111RawActivitySourceAnalyzer(),
            "QYL0111",
            RawSource,
            new Dictionary<string, string> { ["build_property.OtelSemConvInstrumentationLibrary"] = "true" });

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Is_silent_for_the_generated_starters()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(
            new Qyl0111RawActivitySourceAnalyzer(),
            "QYL0111",
            """
            public static class Checkout
            {
                public static void Run()
                {
                    using var span = Qyl.Spans.Work("checkout");
                }
            }
            """);

        diagnostics.Should().BeEmpty();
    }
}
