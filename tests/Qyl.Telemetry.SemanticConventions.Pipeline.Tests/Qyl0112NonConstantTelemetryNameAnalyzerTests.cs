using Qyl.Telemetry.SemanticConventions.Analyzers;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;
/// <summary>QYL0112: a Work name is a compile-time constant (low cardinality).</summary>
[TestClass]
public sealed class Qyl0112NonConstantTelemetryNameAnalyzerTests(TestContext testContext)
{

    [TestMethod]
    public async Task Reports_a_composed_work_name_without_restricting_native_attribute_keys()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(new Qyl0112NonConstantTelemetryNameAnalyzer(), "QYL0112", """
            using Qyl;

            public static class Store
            {
                public static void Write(string what, string key)
                {
                    using var span = Spans.Work($"write {what}");
                    span?.SetTag(key, "ord-1042");
                }
            }
            """, cancellationToken: testContext.CancellationToken);
        Assert.HasCount(1, diagnostics);
    }

    [TestMethod]
    public async Task Is_silent_for_constants()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(new Qyl0112NonConstantTelemetryNameAnalyzer(), "QYL0112", """
            using Qyl;

            public static class Store
            {
                private const string Name = "write orders";

                public static void Write(string id)
                {
                    using var span = Spans.Work(Name);
                    span?.SetTag("order.id", id).SetTag("order.lines", 3L);
                }
            }
            """, cancellationToken: testContext.CancellationToken);
        Assert.IsEmpty(diagnostics);
    }
}
