using System.Diagnostics;
using ANcpLua.Roslyn.Utilities.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Qyl.Telemetry.SemanticConventions.Analyzers;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

[TestClass]
public sealed class Qyl0006SchemaUrlAttributeAnalyzerTests(TestContext testContext)
{

    [TestMethod]
    [DataRow("\"telemetry.schema_url\"")]
    [DataRow("SchemaAttribute")]
    public async Task Reports_a_schema_url_written_as_a_resource_attribute(string key)
    {
        var source = SourceFixture.InMethod($$"""
            ResourceBuilder.CreateEmpty().AddAttributes(new[] { new KeyValuePair<string, object>({{key}}, "https://example.test/schema") });
            """, declarations: "using System.Collections.Generic; using OpenTelemetry.Resources;",
            members: "private const string SchemaAttribute = \"telemetry.schema_url\";");
        var diagnostics = await CompileResource(source).WithAnalyzers([new Qyl0006SchemaUrlAttributeAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(testContext.CancellationToken);
        var diagnostic = Assert.ContainsSingle(diagnostics);
        Assert.AreEqual("QYL0006", diagnostic.Id);
        Assert.AreEqual(key, source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
    }

    [TestMethod]
    [DataRow("ResourceBuilder.CreateEmpty().AddAttributes(new[] { new KeyValuePair<string, object>(\"service.name\", \"checkout\") });")]
    [DataRow("ResourceBuilder.CreateEmpty().AddAttributes(new[] { new KeyValuePair<string, object>(\"service.name\", \"checkout\") }, schemaUrl: \"https://example.test/schema\");")]
    public async Task Accepts_unknown_schemas_and_explicit_schema_metadata(string statement)
    {
        var source = SourceFixture.InMethod(statement,
            declarations: "using System.Collections.Generic; using OpenTelemetry.Resources;");
        var diagnostics = await CompileResource(source).WithAnalyzers([new Qyl0006SchemaUrlAttributeAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(testContext.CancellationToken);
        Assert.IsEmpty(diagnostics);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("https://example.test/schema")]
    public void Export_uses_explicit_resource_schema_metadata_and_never_the_attribute(string? schema)
    {
        using var source = new ActivitySource($"schema-test-{Guid.NewGuid():N}");
        using var exporter = new SchemaExporter();
        var resource = ResourceBuilder.CreateEmpty().AddAttributes(
            new Dictionary<string, object> { ["service.name"] = "checkout", ["telemetry.schema_url"] = "misplaced" }, schema);
        using var provider = Sdk.CreateTracerProviderBuilder().AddSource(source.Name)
            .SetResourceBuilder(resource).AddProcessor(new SimpleActivityExportProcessor(exporter)).Build();
        ActivityTraceId traceId;
        ActivitySpanId spanId;
        using (var activity = source.StartActivity("schema-export", ActivityKind.Internal, default(ActivityContext)))
        {
            Assert.IsNotNull(activity);
            traceId = activity.TraceId;
            spanId = activity.SpanId;
            activity.SetStatus(ActivityStatusCode.Ok);
        }

        var exported = Assert.ContainsSingle(exporter.Exports);
        Assert.AreEqual(traceId, exported.Activity.TraceId);
        Assert.AreEqual(spanId, exported.Activity.SpanId);
        Assert.AreEqual("schema-export", exported.Activity.DisplayName);
        Assert.AreEqual(ActivityKind.Internal, exported.Activity.Kind);
        Assert.AreEqual(ActivityStatusCode.Ok, exported.Activity.Status);
        Assert.AreEqual(source.Name, exported.Activity.Source.Name);
        Assert.AreEqual(schema, exported.Resource.SchemaUrl);
        Assert.AreEqual("checkout", exported.Resource.Attributes.Single(item => item.Key == "service.name").Value);
        Assert.AreEqual("misplaced", exported.Resource.Attributes.Single(item => item.Key == "telemetry.schema_url").Value);
    }

    private static Microsoft.CodeAnalysis.CSharp.CSharpCompilation CompileResource(string source) =>
        AnalyzerRun.CompileSource(Compile.Source(source).WithReference<ResourceBuilder>());

    private sealed class SchemaExporter : BaseExporter<Activity>
    {
        public List<(Activity Activity, Resource Resource)> Exports { get; } = [];

        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
                Exports.Add((activity, ParentProvider.GetResource()));
            return ExportResult.Success;
        }
    }
}
