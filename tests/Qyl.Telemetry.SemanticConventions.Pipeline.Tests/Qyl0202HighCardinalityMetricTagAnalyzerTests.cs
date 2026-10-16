using System.Diagnostics.Metrics;
using System.Globalization;
using ANcpLua.Roslyn.Utilities.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.Telemetry.SemanticConventions.Analyzers;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;
[TestClass]
public sealed class Qyl0202HighCardinalityMetricTagAnalyzerTests(TestContext testContext)
{

    [TestMethod]
    [DataRow("counter.Add(1, new KeyValuePair<string, object?>(\"user.id\", 42));", "user.id")]
    [DataRow("histogram.Record(1.5, new KeyValuePair<string, object?>(\"url.full\", \"https://example.test\"));", "url.full")]
    [DataRow("upDown.Add(1, new KeyValuePair<string, object?>(\"transaction.id\", \"tx-1\"));", "transaction.id")]
    [DataRow("_ = new Measurement<int>(1, new KeyValuePair<string, object?>(\"customer.email\", \"a@example.test\"));", "customer.email")]
    [DataRow("var tags = new TagList(); tags.Add(\"request.id\", 1); counter.Add(1, tags);", "request.id")]
    [DataRow("var tags = new TagList { { \"sessionId\", 1 } }; counter.Add(1, tags);", "sessionId")]
    public async Task Reports_a_high_cardinality_key_at_a_real_metric_measurement(string statement, string expectedKey)
    {
        var compilation = AnalyzerRun.CompileSource(Compile.Source(Fixture(statement)).WithReference<Meter>());
        var diagnostics = await compilation.WithAnalyzers([new Qyl0202HighCardinalityMetricTagAnalyzer()]).GetAnalyzerDiagnosticsAsync(testContext.CancellationToken);
        var diagnostic = Assert.ContainsSingle(diagnostics);
        Assert.AreEqual("QYL0202", diagnostic.Id);
        Assert.Contains(expectedKey, diagnostic.GetMessage(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    [DataRow("counter.Add(1, new KeyValuePair<string, object?>(\"http.route\", \"/orders/{id}\"));")]
    [DataRow("counter.Add(1, new KeyValuePair<string, object?>(\"enduser.id\", 42));")]
    [DataRow("activity.SetTag(\"user.id\", 42);")]
    [DataRow("var tags = new TagList(); tags.Add(\"request.id\", 1); source.StartActivity(\"operation\", ActivityKind.Internal, default(ActivityContext), tags);")]
    public async Task Accepts_bounded_metric_keys_and_keeps_span_tags_separate(string statement)
    {
        var compilation = AnalyzerRun.CompileSource(Compile.Source(Fixture(statement)).WithReference<Meter>());
        var diagnostics = await compilation.WithAnalyzers([new Qyl0202HighCardinalityMetricTagAnalyzer()]).GetAnalyzerDiagnosticsAsync(testContext.CancellationToken);
        Assert.IsEmpty(diagnostics);
    }

    private static string Fixture(string statement) => SourceFixture.InMethod(statement, declarations: "using System.Diagnostics; using System.Diagnostics.Metrics; using System.Collections.Generic;", members: """
            private static readonly Meter Meter = new("application");
            private readonly Counter<int> counter = Meter.CreateCounter<int>("orders");
            private readonly Histogram<double> histogram = Meter.CreateHistogram<double>("duration");
            private readonly UpDownCounter<int> upDown = Meter.CreateUpDownCounter<int>("active");
            private readonly ActivitySource source = new("application");
            private readonly Activity activity = new("operation");
            """);
}
