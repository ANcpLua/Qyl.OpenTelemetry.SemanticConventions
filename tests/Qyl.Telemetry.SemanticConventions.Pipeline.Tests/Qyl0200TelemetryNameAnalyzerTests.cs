using System.Diagnostics;
using System.Globalization;
using ANcpLua.Roslyn.Utilities.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.Telemetry.SemanticConventions.Analyzers;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;
/// <summary>
/// QYL0200 behavior: reserved qyl names use the generated catalog while
/// custom application and library telemetry remains valid.
/// </summary>
[TestClass]
public sealed class Qyl0200TelemetryNameAnalyzerTests(TestContext testContext)
{

    [TestMethod]
    public async Task Qyl0200_fails_the_build_for_a_hardcoded_unknown_attribute_key()
    {
        var diagnostics = await AnalyzerRun.CompileSource(Compile.Source(ActivitySink("activity?.SetTag(\"qyl.invented.attribute\", 1);")).WithReference<Activity>()).WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()]).GetAnalyzerDiagnosticsAsync(testContext.CancellationToken);
        Assert.ContainsSingle(diagnostics);
        Assert.AreEqual("QYL0200", diagnostics[0].Id);
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostics[0].Severity);
        Assert.Contains("qyl.invented.attribute", diagnostics[0].GetMessage(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public async Task Qyl0200_accepts_registry_attribute_keys_including_qyl_owned_entries()
    {
        var diagnostics = await AnalyzerRun.CompileSource(Compile.Source(ActivitySink("""
                activity?.SetTag("http.request.method", "GET");
                activity?.SetTag("qyl.instrumentation.domain", "http");
                activity?.SetTag("qyl.capability.id", "capability-1");
                activity?.SetTag("qyl.capability.kind", "instrumentation");
                activity?.SetTag("qyl.exception.source", "error-capture");
                activity?.SetTag("qyl.project.id", "project-1");
                activity?.SetTag("qyl.workspace.id", "workspace-1");
                activity?.SetTag("qyl.mcp.server.id", "server-1");
                activity?.SetTag("qyl.mcp.tool.name", "search");
                """)).WithReference<Activity>()).WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()]).GetAnalyzerDiagnosticsAsync(testContext.CancellationToken);
        Assert.IsEmpty(diagnostics);
    }

    [TestMethod]
    public async Task Qyl0200_resolves_hand_written_constants_through_constant_propagation()
    {
        var diagnostics = await AnalyzerRun.CompileSource(Compile.Source(ActivitySink("""
                const string invented = "qyl.laundered.attribute";
                activity?.SetTag(invented, 1);
                """)).WithReference<Activity>()).WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()]).GetAnalyzerDiagnosticsAsync(testContext.CancellationToken);
        Assert.ContainsSingle(diagnostics);
        Assert.AreEqual("QYL0200", diagnostics[0].Id);
    }

    [TestMethod]
    public async Task Qyl0200_ignores_non_constant_name_expressions()
    {
        var diagnostics = await AnalyzerRun.CompileSource(Compile.Source(ActivitySink("""
                var dynamicKey = "qyl." + activity?.DisplayName;
                activity?.SetTag(dynamicKey!, 1);
                """)).WithReference<Activity>()).WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()]).GetAnalyzerDiagnosticsAsync(testContext.CancellationToken);
        Assert.IsEmpty(diagnostics);
    }

    [TestMethod]
    public async Task Qyl0200_checks_activity_source_construction_against_scope_names()
    {
        var clean = await AnalyzerRun.CompileSource(Compile.Source("""
                using System.Diagnostics;
                internal static class Owner
                {
                    public static readonly ActivitySource Source = new("Qyl.Collector");
                }
                """).WithReference<Activity>()).WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()]).GetAnalyzerDiagnosticsAsync(testContext.CancellationToken);
        Assert.IsEmpty(clean);
        var flagged = await AnalyzerRun.CompileSource(Compile.Source("""
                using System.Diagnostics;
                internal static class Owner
                {
                    public static readonly ActivitySource Source = new("Qyl.Unregistered.Scope");
                }
                """).WithReference<Activity>()).WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()]).GetAnalyzerDiagnosticsAsync(testContext.CancellationToken);
        Assert.ContainsSingle(flagged);
        Assert.AreEqual("QYL0200", flagged[0].Id);
        Assert.Contains("instrumentation scope name", flagged[0].GetMessage(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public async Task Qyl0200_checks_meter_instrument_names_against_catalog_metrics()
    {
        var clean = await AnalyzerRun.CompileSource(Compile.Source(MeterSink("Meter.CreateHistogram<double>(\"db.client.operation.duration\", \"s\");")).WithReference<Activity>()).WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()]).GetAnalyzerDiagnosticsAsync(testContext.CancellationToken);
        Assert.IsEmpty(clean);
        var flagged = await AnalyzerRun.CompileSource(Compile.Source(MeterSink("Meter.CreateCounter<long>(\"qyl.invented.metric\");")).WithReference<Activity>()).WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()]).GetAnalyzerDiagnosticsAsync(testContext.CancellationToken);
        Assert.ContainsSingle(flagged);
        Assert.AreEqual("QYL0200", flagged[0].Id);
        Assert.Contains("metric name", flagged[0].GetMessage(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public async Task Qyl0200_checks_activity_event_names()
    {
        var clean = await AnalyzerRun.CompileSource(Compile.Source(ActivitySink("""
                activity?.AddEvent(new ActivityEvent("exception"));
                activity?.AddEvent(new ActivityEvent("qyl.rpc.grpc"));
                """)).WithReference<Activity>()).WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()]).GetAnalyzerDiagnosticsAsync(testContext.CancellationToken);
        Assert.IsEmpty(clean);
        var flagged = await AnalyzerRun.CompileSource(Compile.Source(ActivitySink("activity?.AddEvent(new ActivityEvent(\"qyl.invented.event\"));")).WithReference<Activity>()).WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()]).GetAnalyzerDiagnosticsAsync(testContext.CancellationToken);
        Assert.ContainsSingle(flagged);
        Assert.Contains("event name", flagged[0].GetMessage(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public async Task Qyl0200_trusts_generated_code()
    {
        var compilation = AnalyzerRun.CompileSource(Compile.Source(ActivitySink("activity?.SetTag(\"qyl.invented.attribute\", 1);")).WithReference<Activity>());
        var tree = compilation.SyntaxTrees.Single();
        var diagnostics = await compilation.ReplaceSyntaxTree(tree, tree.WithFilePath("/repo/Consumer.g.cs")).WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()]).GetAnalyzerDiagnosticsAsync(testContext.CancellationToken);
        Assert.IsEmpty(diagnostics);
    }

    [TestMethod]
    public async Task Qyl0200_accepts_custom_application_and_library_telemetry()
    {
        var diagnostics = await AnalyzerRun.CompileSource(Compile.Source(ActivitySink("""
                activity?.SetTag("checkout.order.id", "ord-42");
                activity?.AddEvent(new ActivityEvent("checkout.order.placed"));
                activity?.AddEvent(new ActivityEvent("qyl.app.order.placed"));
                using var source = new ActivitySource("Checkout.Application");
                using var meter = new System.Diagnostics.Metrics.Meter("Checkout.Metrics");
                meter.CreateCounter<long>("checkout.orders.created", "{order}");
                meter.CreateCounter<long>("qyl.app.orders.created", "{order}");
                """)).WithReference<Activity>()).WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()]).GetAnalyzerDiagnosticsAsync(testContext.CancellationToken);
        Assert.IsEmpty(diagnostics);
    }

    [TestMethod]
    public async Task Qyl0200_accepts_app_keys_and_the_application_scope()
    {
        var diagnostics = await AnalyzerRun.CompileSource(Compile.Source(ActivitySink("""
                activity?.SetTag("qyl.app.order.id", "ord-1042");
                using var source = new ActivitySource("Qyl.Telemetry.Application");
                """)).WithReference<Activity>()).WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()]).GetAnalyzerDiagnosticsAsync(testContext.CancellationToken);
        Assert.IsEmpty(diagnostics);
    }

    private static string ActivitySink(string statements) => SourceFixture.InMethod(statements, declarations: "using System.Diagnostics;", parameters: "Activity? activity");
    private static string MeterSink(string statements) => SourceFixture.InMethod(statements, declarations: "using System.Diagnostics.Metrics;", members: "private static readonly Meter Meter = new(\"System.Runtime\");");
}
