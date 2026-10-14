using System.Diagnostics;
using System.Globalization;
using ANcpLua.Roslyn.Utilities.Testing;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.Telemetry.SemanticConventions.Analyzers;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
/// QYL0200/QYL0201 behavior, including the G4 negative proof: a hardcoded telemetry
/// name in a name position produces an error-severity diagnostic, so the build fails.
/// The allowlist under test is the generated <c>SemconvRegistryFacts</c>, never a
/// list maintained in the analyzer or in these tests.
/// </summary>
public sealed class Qyl0200TelemetryNameAnalyzerTests
{
    [Fact]
    public async Task Qyl0200_fails_the_build_for_a_hardcoded_unknown_attribute_key()
    {
        var diagnostics = await Compile
            .Source(ActivitySink("activity?.SetTag(\"qyl.invented.attribute\", 1);"))
            .WithReference<Activity>()
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("QYL0200");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("qyl.invented.attribute");
    }

    [Fact]
    public async Task Qyl0200_accepts_registry_attribute_keys_including_qyl_owned_entries()
    {
        var diagnostics = await Compile
            .Source(ActivitySink(
                """
                activity?.SetTag("http.request.method", "GET");
                activity?.SetTag("qyl.instrumentation.domain", "http");
                activity?.SetTag("qyl.capability.id", "capability-1");
                activity?.SetTag("qyl.capability.kind", "instrumentation");
                activity?.SetTag("qyl.exception.source", "error-capture");
                activity?.SetTag("qyl.project.id", "project-1");
                activity?.SetTag("qyl.workspace.id", "workspace-1");
                activity?.SetTag("qyl.mcp.server.id", "server-1");
                activity?.SetTag("qyl.mcp.tool.name", "search");
                """))
            .WithReference<Activity>()
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0200_resolves_hand_written_constants_through_constant_propagation()
    {
        var diagnostics = await Compile
            .Source(ActivitySink(
                """
                const string invented = "qyl.laundered.attribute";
                activity?.SetTag(invented, 1);
                """))
            .WithReference<Activity>()
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("QYL0200");
    }

    [Fact]
    public async Task Qyl0200_ignores_non_constant_name_expressions()
    {
        var diagnostics = await Compile
            .Source(ActivitySink(
                """
                var dynamicKey = "qyl." + activity?.DisplayName;
                activity?.SetTag(dynamicKey!, 1);
                """))
            .WithReference<Activity>()
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0200_checks_activity_source_construction_against_scope_names()
    {
        var clean = await Compile
            .Source(
                """
                using System.Diagnostics;
                internal static class Owner
                {
                    public static readonly ActivitySource Source = new("Qyl.Collector");
                }
                """)
            .WithReference<Activity>()
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        clean.Should().BeEmpty();

        var flagged = await Compile
            .Source(
                """
                using System.Diagnostics;
                internal static class Owner
                {
                    public static readonly ActivitySource Source = new("Not.A.Registered.Scope");
                }
                """)
            .WithReference<Activity>()
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        flagged.Should().ContainSingle();
        flagged[0].Id.Should().Be("QYL0200");
        flagged[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("instrumentation scope name");
    }

    [Fact]
    public async Task Qyl0200_checks_meter_instrument_names_against_catalog_metrics()
    {
        var clean = await Compile
            .Source(MeterSink("Meter.CreateHistogram<double>(\"db.client.operation.duration\", \"s\");"))
            .WithReference<Activity>()
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        clean.Should().BeEmpty();

        var flagged = await Compile
            .Source(MeterSink("Meter.CreateCounter<long>(\"qyl.invented.metric\");"))
            .WithReference<Activity>()
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        flagged.Should().ContainSingle();
        flagged[0].Id.Should().Be("QYL0200");
        flagged[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("metric name");
    }

    [Fact]
    public async Task Qyl0200_checks_activity_event_names()
    {
        var clean = await Compile
            .Source(ActivitySink(
                """
                activity?.AddEvent(new ActivityEvent("exception"));
                activity?.AddEvent(new ActivityEvent("qyl.rpc.grpc"));
                """))
            .WithReference<Activity>()
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        clean.Should().BeEmpty();

        var flagged = await Compile
            .Source(ActivitySink("activity?.AddEvent(new ActivityEvent(\"qyl.invented.event\"));"))
            .WithReference<Activity>()
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        flagged.Should().ContainSingle();
        flagged[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("event name");
    }

    [Fact]
    public async Task Qyl0200_trusts_generated_code()
    {
        var compilation = Compile
            .Source(ActivitySink("activity?.SetTag(\"qyl.invented.attribute\", 1);"))
            .WithReference<Activity>()
            .Build()
            .ShouldSucceed()
            .Compilation;
        var tree = compilation.SyntaxTrees.Single();

        var diagnostics = await compilation
            .ReplaceSyntaxTree(tree, tree.WithFilePath("/repo/Consumer.g.cs"))
            .WithAnalyzers([new Qyl0200TelemetryNameAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0201_requires_descriptor_metric_names_to_be_catalog_members()
    {
        var flagged = await Compile
            .Source(DescriptorSink("qyl.invented.metric"))
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0201InvalidMetricNameAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        flagged.Should().ContainSingle();
        flagged[0].Id.Should().Be("QYL0201");
        flagged[0].Severity.Should().Be(DiagnosticSeverity.Error);

        var clean = await Compile
            .Source(DescriptorSink("gen_ai.client.inference.operation.input_tokens"))
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0201InvalidMetricNameAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        clean.Should().BeEmpty();
    }

    [Theory]
    [InlineData("quartz.job.execution.active")]
    [InlineData("quartz.job.execution.duration")]
    [InlineData("quartz.trigger.retry")]
    public async Task Qyl0201_accepts_native_Quartz_metrics(string name)
    {
        var diagnostics = await Compile
            .Source(DescriptorSink(name))
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0201InvalidMetricNameAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().BeEmpty();
    }

    private static string ActivitySink(string statements) =>
        SourceFixture.InMethod(statements, declarations: "using System.Diagnostics;", parameters: "Activity? activity");

    private static string MeterSink(string statements) =>
        SourceFixture.InMethod(
            statements,
            declarations: "using System.Diagnostics.Metrics;",
            members: "private static readonly Meter Meter = new(\"System.Runtime\");");

    private static string DescriptorSink(string metricName) =>
        $$"""
        #nullable enable
        namespace Qyl.Instrumentation.Instrumentation
        {
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class CounterAttribute(string name) : System.Attribute
            {
                public string Name { get; } = name;
            }
        }

        internal sealed class Sink
        {
            [Qyl.Instrumentation.Instrumentation.Counter("{{metricName}}")]
            internal void Record() { }
        }
        """;
}
