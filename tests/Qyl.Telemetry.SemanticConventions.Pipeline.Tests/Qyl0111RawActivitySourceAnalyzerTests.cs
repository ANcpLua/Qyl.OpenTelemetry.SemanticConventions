using System.Globalization;
using Qyl.Telemetry.SemanticConventions.Analyzers;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;
/// <summary>QYL0111: application code starts spans only through the generated starters (ADR D2).</summary>
[TestClass]
public sealed class Qyl0111RawActivitySourceAnalyzerTests(TestContext testContext)
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
    [TestMethod]
    public async Task Reports_a_new_ActivitySource_and_a_raw_StartActivity()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(new Qyl0111RawActivitySourceAnalyzer(), "QYL0111", RawSource, cancellationToken: testContext.CancellationToken);
        Assert.HasCount(2, diagnostics);
        Assert.IsTrue(diagnostics.All(static d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
        Assert.IsTrue(diagnostics.Select(static d => d.GetMessage(CultureInfo.InvariantCulture)).Any(static m => m.Contains("ActivitySource", StringComparison.Ordinal)));
        Assert.IsTrue(diagnostics.Select(static d => d.GetMessage(CultureInfo.InvariantCulture)).Any(static m => m.Contains("StartActivity", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Is_silent_in_an_instrumentation_library()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(new Qyl0111RawActivitySourceAnalyzer(), "QYL0111", RawSource, new Dictionary<string, string> { ["build_property.OtelSemConvInstrumentationLibrary"] = "true" }, cancellationToken: testContext.CancellationToken);
        Assert.IsEmpty(diagnostics);
    }

    [TestMethod]
    public async Task Is_silent_for_the_generated_starters()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(new Qyl0111RawActivitySourceAnalyzer(), "QYL0111", """
            public static class Checkout
            {
                public static void Run()
                {
                    using var span = Qyl.Spans.Work("checkout");
                }
            }
            """, cancellationToken: testContext.CancellationToken);
        Assert.IsEmpty(diagnostics);
    }
}
