using System.Collections.Immutable;
using System.Globalization;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.Telemetry.SemanticConventions.Analyzers;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

public sealed class RegistryDerivedAnalyzerTests
{
    [Fact]
    public async Task Qyl0013_uses_attribute_types_outside_the_old_handwritten_subset()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0013IncorrectAttributeTypeAnalyzer(),
            SinkSource("SetTag(\"gen_ai.memory.record.id\", 42);"));

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("QYL0013");
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("string");
    }

    [Fact]
    public async Task Qyl0013_accepts_registry_array_and_schema_governed_any_types()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0013IncorrectAttributeTypeAnalyzer(),
            SinkSource(
                """
                SetTag("gen_ai.response.finish_reasons", new[] { "stop" });
                SetTag("gen_ai.input.messages", new object());
                """));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0012_reports_only_noncanonical_spelling_of_known_enum_values()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0012InvalidAttributeValueAnalyzer(),
            SinkSource("SetTag(\"http.request.method\", \"get\");"));

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("QYL0012");
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("'GET'");
    }

    [Fact]
    public async Task Qyl0012_allows_unknown_extension_enum_values()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0012InvalidAttributeValueAnalyzer(),
            SinkSource("SetTag(\"http.request.method\", \"CUSTOM\");"));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0403_uses_the_complete_generated_operation_enum()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0012InvalidAttributeValueAnalyzer(),
            SinkSource("SetTag(\"gen_ai.operation.name\", \"Execute_Tool\");"));

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("execute_tool");
    }

    [Fact]
    public async Task Qyl0403_accepts_new_memory_and_custom_operations()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0012InvalidAttributeValueAnalyzer(),
            SinkSource(
                """
                SetTag("gen_ai.operation.name", "create_memory_store");
                SetTag("gen_ai.operation.name", "vendor_specific_operation");
                """));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0402_accepts_every_registry_defined_token_metric()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0402UseTokenUsageHistogramAnalyzer(),
            HistogramSource("gen_ai.server.time_per_output_token"));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0402_reports_unknown_genai_token_metrics()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0402UseTokenUsageHistogramAnalyzer(),
            HistogramSource("gen_ai.custom.token.count"));

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("gen_ai.client.token.usage");
    }

    [Fact]
    public async Task Qyl0400_uses_generated_execute_tool_requirements()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new GenAiExecuteToolNameAnalyzer(),
            SinkSource("SetTag(\"gen_ai.operation.name\", \"execute_tool\");"));

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("gen_ai.tool.name");
    }

    [Fact]
    public async Task Qyl0400_is_silent_when_generated_execute_tool_requirement_is_present()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new GenAiExecuteToolNameAnalyzer(),
            SinkSource(
                """
                SetTag("gen_ai.operation.name", "execute_tool");
                SetTag("gen_ai.tool.name", "weather");
                """));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0401_applies_the_internal_execute_tool_span_requirements()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0401GenAiMissingRequiredAttributesAnalyzer(),
            ActivitySource(
                "ActivityKind.Internal",
                "SetTag(\"gen_ai.operation.name\", \"execute_tool\");"));

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("gen_ai.tool.name");
    }

    [Fact]
    public async Task Qyl0401_applies_the_client_inference_requirements()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0401GenAiMissingRequiredAttributesAnalyzer(),
            ActivitySource(
                "ActivityKind.Client",
                "SetTag(\"gen_ai.operation.name\", \"chat\");"));

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("gen_ai.provider.name");
    }

    [Fact]
    public async Task Qyl0401_applies_provider_refinement_requirements()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0401GenAiMissingRequiredAttributesAnalyzer(),
            ActivitySource(
                "ActivityKind.Client",
                """
                SetTag("gen_ai.operation.name", "chat");
                SetTag("gen_ai.provider.name", "openai");
                """));

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("gen_ai.request.model");
    }

    [Fact]
    public async Task Qyl0401_does_not_promote_conditional_model_to_universally_required()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0401GenAiMissingRequiredAttributesAnalyzer(),
            ActivitySource(
                "ActivityKind.Client",
                """
                SetTag("gen_ai.operation.name", "chat");
                SetTag("gen_ai.provider.name", "custom.provider");
                """));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0401_resolves_mcp_client_spans_separately()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0401GenAiMissingRequiredAttributesAnalyzer(),
            ActivitySource(
                "ActivityKind.Client",
                "SetTag(\"mcp.method.name\", \"tools/call\");"));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0401_accepts_memory_operations_added_by_the_pinned_registry()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0401GenAiMissingRequiredAttributesAnalyzer(),
            ActivitySource(
                "ActivityKind.Client",
                "SetTag(\"gen_ai.operation.name\", \"create_memory_store\");"));

        diagnostics.Should().BeEmpty();
    }

    private static string SinkSource(string statements) =>
        $$"""
        #nullable enable
        internal sealed class Sink
        {
            private void SetTag(string key, object? value) { }
            internal void Emit()
            {
                {{statements}}
            }
        }
        """;

    private static string ActivitySource(string kind, string statements) =>
        $$"""
        #nullable enable
        using System.Diagnostics;
        internal sealed class Instrumentation
        {
            private static readonly ActivitySource Source = new("tests");
            internal void Emit()
            {
                using var activity = Source.StartActivity("operation", {{kind}});
                void SetTag(string key, object? value) => activity?.SetTag(key, value);
                {{statements}}
            }
        }
        """;

    private static string HistogramSource(string metricName) =>
        $$"""
        using System;
        namespace Qyl.Instrumentation.Instrumentation
        {
            [AttributeUsage(AttributeTargets.Method)]
            public sealed class HistogramAttribute(string name) : Attribute;
        }

        internal sealed class Metrics
        {
            [Qyl.Instrumentation.Instrumentation.Histogram("{{metricName}}")] 
            internal void Record() { }
        }
        """;
}
