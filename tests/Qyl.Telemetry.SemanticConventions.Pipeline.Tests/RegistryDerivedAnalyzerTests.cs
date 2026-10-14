using System.Diagnostics;
using System.Globalization;
using ANcpLua.Roslyn.Utilities.Testing;
using AwesomeAssertions;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.Telemetry.SemanticConventions.Analyzers;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

public sealed class RegistryDerivedAnalyzerTests
{
    [Fact]
    public async Task Qyl0013_preserves_integer_enum_types()
    {
        var diagnostics = await Compile
            .Source(SinkSource(
                """
                SetTag("cpython.gc.generation", 0L);
                SetTag("cpython.gc.generation", "0");
                SetTag("rpc.grpc.status_code", 0L);
                SetTag("rpc.grpc.status_code", "0");
                """))
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0013IncorrectAttributeTypeAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(static diagnostic => diagnostic.Id == "QYL0013");
    }

    [Fact]
    public async Task Qyl0013_uses_attribute_types_outside_the_old_handwritten_subset()
    {
        var diagnostics = await Compile
            .Source(SinkSource("SetTag(\"gen_ai.memory.record.id\", 42);"))
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0013IncorrectAttributeTypeAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("QYL0013");
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("string");
    }

    [Fact]
    public async Task Qyl0013_accepts_registry_array_and_schema_governed_any_types()
    {
        var diagnostics = await Compile
            .Source(SinkSource(
                """
                SetTag("gen_ai.response.finish_reasons", new[] { "stop" });
                SetTag("gen_ai.input.messages", new object());
                """))
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0013IncorrectAttributeTypeAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0013_enforces_registry_integer_types()
    {
        var diagnostics = await Compile
            .Source(SinkSource(
                """
                SetTag("http.request.method", "GET");
                SetTag("http.request.resend_count", 1);
                SetTag("http.response.status_code", "200");
                """))
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0013IncorrectAttributeTypeAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("QYL0013");
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("integer");
    }

    [Fact]
    public async Task Qyl0012_enforces_qyl_owned_enum_tokens()
    {
        var diagnostics = await Compile
            .Source(SinkSource(
                """
                SetTag("qyl.instrumentation.domain", "HTTP.CLIENT");
                SetTag("qyl.instrumentation.domain", "Messaging.RabbitMq");
                """))
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0012InvalidAttributeValueAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().HaveCount(2);
        diagnostics.Select(static diagnostic => diagnostic.Id).Should().OnlyContain(static id => id == "QYL0012");
        diagnostics.Select(static diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture))
            .Should().Contain(static message => message.Contains("http.client", StringComparison.Ordinal));
        diagnostics.Select(static diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture))
            .Should().Contain(static message => message.Contains("messaging.rabbitmq", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Qyl0012_reports_only_noncanonical_spelling_of_known_enum_values()
    {
        var diagnostics = await Compile
            .Source(SinkSource("SetTag(\"http.request.method\", \"get\");"))
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0012InvalidAttributeValueAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("QYL0012");
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("'GET'");
    }

    [Fact]
    public async Task Qyl0012_allows_unknown_extension_enum_values()
    {
        var diagnostics = await Compile
            .Source(SinkSource("SetTag(\"http.request.method\", \"CUSTOM\");"))
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0012InvalidAttributeValueAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0403_uses_the_complete_generated_operation_enum()
    {
        var diagnostics = await Compile
            .Source(SinkSource("SetTag(\"gen_ai.operation.name\", \"Execute_Tool\");"))
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0012InvalidAttributeValueAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("execute_tool");
    }

    [Fact]
    public async Task Qyl0403_accepts_new_memory_and_custom_operations()
    {
        var diagnostics = await Compile
            .Source(SinkSource(
                """
                SetTag("gen_ai.operation.name", "create_memory_store");
                SetTag("gen_ai.operation.name", "vendor_specific_operation");
                """))
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0012InvalidAttributeValueAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0402_accepts_every_registry_defined_token_metric()
    {
        var diagnostics = await Compile
            .Source(HistogramSource("gen_ai.server.time_per_output_token"))
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0402UseTokenUsageHistogramAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0402_reports_unknown_genai_token_metrics()
    {
        var diagnostics = await Compile
            .Source(HistogramSource("gen_ai.custom.token.count"))
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0402UseTokenUsageHistogramAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("gen_ai.client.inference.operation.input_tokens");
    }

    [Fact]
    public async Task Qyl0400_uses_generated_execute_tool_requirements()
    {
        var diagnostics = await Compile
            .Source(SinkSource("SetTag(\"gen_ai.operation.name\", \"execute_tool\");"))
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new GenAiExecuteToolNameAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("gen_ai.tool.name");
    }

    [Fact]
    public async Task Qyl0400_is_silent_when_generated_execute_tool_requirement_is_present()
    {
        var diagnostics = await Compile
            .Source(SinkSource(
                """
                SetTag("gen_ai.operation.name", "execute_tool");
                SetTag("gen_ai.tool.name", "weather");
                """))
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new GenAiExecuteToolNameAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0401_applies_the_internal_execute_tool_span_requirements()
    {
        var diagnostics = await Compile
            .Source(ActivitySource(
                "ActivityKind.Internal",
                "SetTag(\"gen_ai.operation.name\", \"execute_tool\");"))
            .WithReference<Activity>()
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0401GenAiMissingRequiredAttributesAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("gen_ai.tool.name");
    }

    [Fact]
    public async Task Qyl0401_applies_the_client_inference_requirements()
    {
        var diagnostics = await Compile
            .Source(ActivitySource(
                "ActivityKind.Client",
                "SetTag(\"gen_ai.operation.name\", \"chat\");"))
            .WithReference<Activity>()
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0401GenAiMissingRequiredAttributesAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("gen_ai.provider.name");
    }

    [Fact]
    public async Task Qyl0401_applies_provider_refinement_requirements()
    {
        var diagnostics = await Compile
            .Source(ActivitySource(
                "ActivityKind.Client",
                """
                SetTag("gen_ai.operation.name", "chat");
                SetTag("gen_ai.provider.name", "openai");
                """))
            .WithReference<Activity>()
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0401GenAiMissingRequiredAttributesAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("gen_ai.request.model");
    }

    [Fact]
    public async Task Qyl0401_does_not_promote_conditional_model_to_universally_required()
    {
        var diagnostics = await Compile
            .Source(ActivitySource(
                "ActivityKind.Client",
                """
                SetTag("gen_ai.operation.name", "chat");
                SetTag("gen_ai.provider.name", "custom.provider");
                """))
            .WithReference<Activity>()
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0401GenAiMissingRequiredAttributesAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0401_resolves_mcp_client_spans_separately()
    {
        var diagnostics = await Compile
            .Source(ActivitySource(
                "ActivityKind.Client",
                "SetTag(\"mcp.method.name\", \"tools/call\");"))
            .WithReference<Activity>()
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0401GenAiMissingRequiredAttributesAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Qyl0401_accepts_memory_operations_added_by_the_pinned_registry()
    {
        var diagnostics = await Compile
            .Source(ActivitySource(
                "ActivityKind.Client",
                "SetTag(\"gen_ai.operation.name\", \"create_memory_store\");"))
            .WithReference<Activity>()
            .Build()
            .ShouldSucceed()
            .Compilation
            .WithAnalyzers([new Qyl0401GenAiMissingRequiredAttributesAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.Should().BeEmpty();
    }

    private static string SinkSource(string statements) =>
        SourceFixture.InMethod(statements, members: "private void SetTag(string key, object? value) { }");

    private static string ActivitySource(string kind, string statements) =>
        SourceFixture.InMethod(
            $$"""
            using var activity = Source.StartActivity("operation", {{kind}});
            void SetTag(string key, object? value) => activity?.SetTag(key, value);
            {{statements}}
            """,
            declarations: "using System.Diagnostics;",
            members: "private static readonly ActivitySource Source = new(\"tests\");",
            typeName: "Instrumentation");

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
