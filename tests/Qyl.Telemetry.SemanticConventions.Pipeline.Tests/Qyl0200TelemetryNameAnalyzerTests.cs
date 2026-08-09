using System.Globalization;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
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
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0200TelemetryNameAnalyzer(),
            ActivitySink("activity?.SetTag(\"qyl.invented.attribute\", 1);"));

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("QYL0200");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("qyl.invented.attribute");
    }

    [Fact]
    public async Task Qyl0200_accepts_registry_attribute_keys_including_qyl_owned_entries()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0200TelemetryNameAnalyzer(),
            ActivitySink(
                """
                activity?.SetTag("http.request.method", "GET");
                activity?.SetTag("qyl.instrumentation.domain", "http");
                activity?.SetTag("qyl.agent.diagnostic.extension.id", "qyl.agent.diagnostic.snapshot");
                activity?.SetTag("qyl.agent.diagnostic.format.version", 1);
                activity?.SetTag("qyl.agent.diagnostic.snapshot.id", "snapshot-1");
                activity?.SetTag("qyl.agent.diagnostic.probe.id", "vcs.root.resolve");
                activity?.SetTag("qyl.agent.diagnostic.phase", "checkpoint");
                activity?.SetTag("qyl.agent.diagnostic.outcome", "pass");
                activity?.SetTag("qyl.agent.diagnostic.variable.count", 3);
                activity?.SetTag("qyl.agent.diagnostic.check.count", 2);
                activity?.SetTag("qyl.agent.diagnostic.check.failed_count", 0);
                """));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0200_resolves_hand_written_constants_through_constant_propagation()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0200TelemetryNameAnalyzer(),
            ActivitySink(
                """
                const string invented = "qyl.laundered.attribute";
                activity?.SetTag(invented, 1);
                """));

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("QYL0200");
    }

    [Fact]
    public async Task Qyl0200_ignores_non_constant_name_expressions()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0200TelemetryNameAnalyzer(),
            ActivitySink(
                """
                var dynamicKey = "qyl." + activity?.DisplayName;
                activity?.SetTag(dynamicKey!, 1);
                """));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0200_checks_activity_source_construction_against_scope_names()
    {
        var clean = await AnalyzerHarness.RunAsync(
            new Qyl0200TelemetryNameAnalyzer(),
            """
            using System.Diagnostics;
            internal static class Owner
            {
                public static readonly ActivitySource Source = new("Qyl.Collector");
            }
            """);
        clean.Should().BeEmpty();

        var flagged = await AnalyzerHarness.RunAsync(
            new Qyl0200TelemetryNameAnalyzer(),
            """
            using System.Diagnostics;
            internal static class Owner
            {
                public static readonly ActivitySource Source = new("Not.A.Registered.Scope");
            }
            """);
        flagged.Should().ContainSingle();
        flagged[0].Id.Should().Be("QYL0200");
        flagged[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("instrumentation scope name");
    }

    [Fact]
    public async Task Qyl0200_checks_meter_instrument_names_against_catalog_metrics()
    {
        var clean = await AnalyzerHarness.RunAsync(
            new Qyl0200TelemetryNameAnalyzer(),
            MeterSink("Meter.CreateHistogram<double>(\"db.client.operation.duration\", \"s\");"));
        clean.Should().BeEmpty();

        var flagged = await AnalyzerHarness.RunAsync(
            new Qyl0200TelemetryNameAnalyzer(),
            MeterSink("Meter.CreateCounter<long>(\"qyl.invented.metric\");"));
        flagged.Should().ContainSingle();
        flagged[0].Id.Should().Be("QYL0200");
        flagged[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("metric name");
    }

    [Fact]
    public async Task Qyl0200_checks_activity_event_names()
    {
        var clean = await AnalyzerHarness.RunAsync(
            new Qyl0200TelemetryNameAnalyzer(),
            ActivitySink(
                """
                activity?.AddEvent(new ActivityEvent("exception"));
                activity?.AddEvent(new ActivityEvent("qyl.agent.diagnostic.snapshot"));
                """));
        clean.Should().BeEmpty();

        var flagged = await AnalyzerHarness.RunAsync(
            new Qyl0200TelemetryNameAnalyzer(),
            ActivitySink("activity?.AddEvent(new ActivityEvent(\"qyl.invented.event\"));"));
        flagged.Should().ContainSingle();
        flagged[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("event name");
    }

    [Fact]
    public async Task Qyl0200_trusts_generated_code()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0200TelemetryNameAnalyzer(),
            ActivitySink("activity?.SetTag(\"qyl.invented.attribute\", 1);"),
            "/repo/Consumer.g.cs");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0201_requires_descriptor_metric_names_to_be_catalog_members()
    {
        var flagged = await AnalyzerHarness.RunAsync(
            new Qyl0201InvalidMetricNameAnalyzer(),
            DescriptorSink("qyl.invented.metric"));
        flagged.Should().ContainSingle();
        flagged[0].Id.Should().Be("QYL0201");
        flagged[0].Severity.Should().Be(DiagnosticSeverity.Error);

        var clean = await AnalyzerHarness.RunAsync(
            new Qyl0201InvalidMetricNameAnalyzer(),
            DescriptorSink("gen_ai.client.token.usage"));
        clean.Should().BeEmpty();
    }

    private static string ActivitySink(string statements) =>
        $$"""
        #nullable enable
        using System.Diagnostics;
        internal sealed class Sink
        {
            internal void Emit(Activity? activity)
            {
                {{statements}}
            }
        }
        """;

    private static string MeterSink(string statements) =>
        $$"""
        #nullable enable
        using System.Diagnostics.Metrics;
        internal sealed class Sink
        {
            private static readonly Meter Meter = new("System.Runtime");
            internal void Emit()
            {
                {{statements}}
            }
        }
        """;

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
