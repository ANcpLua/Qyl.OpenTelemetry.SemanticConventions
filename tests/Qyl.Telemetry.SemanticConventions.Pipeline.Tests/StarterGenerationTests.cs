using System.Diagnostics;
using System.Reflection;
using ANcpLua.Roslyn.Utilities.Testing;
using AwesomeAssertions;
using Qyl;
using Qyl.Telemetry.SemanticConventions.Activities;
using Qyl.Telemetry.SemanticConventions.Incubating.Activities;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

// Inside Qyl.Telemetry.SemanticConventions.* the simple name Spans is the definitions namespace.
using Spans = global::Qyl.Spans;

/// <summary>
/// Generated span starters (ADR D2): parameters follow the requirement levels mechanically, the
/// kind comes from the group, the name from the <c>qyl.span_name</c> chain, the source is the
/// registry-owned application scope, and the group's <c>qyl.instrumentation.domain</c> is stamped.
/// </summary>
[Collection(ApplicationSourceListeners.Name)]
public sealed class StarterGenerationTests
{
    private static ParameterInfo[] DbClientParameters() =>
        typeof(Spans).GetMethod(nameof(Spans.DbClient))!.GetParameters();

    [Fact]
    public void The_required_attribute_is_positional_and_typed()
    {
        var system = DbClientParameters()[0];

        system.Name.Should().Be("system");
        system.ParameterType.Should().Be<DbSystemName>();
        system.IsOptional.Should().BeFalse();
    }

    [Fact]
    public void Conditionally_required_parameters_are_nullable_and_have_no_default()
    {
        var parameters = DbClientParameters().ToDictionary(static p => p.Name!);

        foreach (var name in new[] { "namespace", "collectionName", "operationName", "serverPort" })
        {
            parameters.Should().ContainKey(name);
            parameters[name].IsOptional.Should().BeFalse($"{name} is conditionally required");
            var type = parameters[name].ParameterType;
            (type == typeof(string) || Nullable.GetUnderlyingType(type) is not null).Should().BeTrue($"{name} is nullable");
        }
    }

    [Fact]
    public void Recommended_parameters_default_to_null()
    {
        var parameters = DbClientParameters().ToDictionary(static p => p.Name!);

        foreach (var name in new[] { "querySummary", "queryText", "storedProcedureName", "serverAddress", "operationBatchSize", "networkPeerAddress", "networkPeerPort" })
        {
            parameters.Should().ContainKey(name);
            parameters[name].IsOptional.Should().BeTrue($"{name} is recommended");
            parameters[name].DefaultValue.Should().BeNull();
        }
    }

    [Fact]
    public void OptIn_and_end_phase_attributes_get_no_parameter()
    {
        DbClientParameters().Select(static p => p.Name).Should()
            .NotContain("queryParameter")
            .And.NotContain("responseReturnedRows")
            .And.NotContain("responseStatusCode")
            .And.NotContain("errorType");
    }

    [Fact]
    public void Omitting_a_conditional_parameter_is_a_compile_error()
    {
        var result = Compile
            .Source("""
                using Qyl;
                using Qyl.Telemetry.SemanticConventions.Activities;

                public static class Store
                {
                    public static void Write() =>
                        Spans.DbClient(DbSystemName.Postgresql, collectionName: "orders", operationName: "write", serverPort: null)?.Dispose();
                }
                """)
            .WithReference(typeof(Spans).Assembly)
            .WithReference<Activity>()
            .Build();

        result.HasError("CS7036").Should().BeTrue();
        result.Diagnostics.Should().Contain(static d => d.Id == "CS7036" && d.GetMessage(null).Contains("namespace", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("SELECT orders", "SELECT", "orders", "shop", null, null, "SELECT orders")]
    [InlineData(null, "write", "orders", "shop", null, null, "write orders")]
    [InlineData(null, "write", null, "shop", null, null, "write shop")]
    [InlineData(null, null, "orders", "shop", null, null, "orders")]
    [InlineData(null, null, null, null, "db.internal", 5432L, "db.internal:5432")]
    [InlineData(null, null, null, null, null, null, "postgresql")]
    public void The_name_follows_the_span_name_chain(
        string? summary, string? operation, string? collection, string? @namespace, string? address, long? port, string expected)
    {
        using var listener = new ApplicationSourceListener();

        using var span = Spans.DbClient(DbSystemName.Postgresql, collectionName: collection, @namespace: @namespace,
            operationName: operation, serverPort: port, querySummary: summary, serverAddress: address);

        span.Should().NotBeNull();
        span!.DisplayName.Should().Be(expected);
    }

    [Fact]
    public void The_starter_starts_a_client_span_on_the_application_scope_with_its_domain()
    {
        using var listener = new ApplicationSourceListener();

        using var span = Spans.DbClient(DbSystemName.Custom("memory"), @namespace: "shop",
            collectionName: "orders", operationName: "write", serverPort: null);

        span!.Kind.Should().Be(ActivityKind.Client);
        span.Source.Name.Should().Be("Qyl.Telemetry.Application");
        span.Source.TelemetrySchemaUrl.Should().Be(SchemaUrl.Qyl);
        span.Source.Version.Should().Be(SchemaUrl.QylVersion);
        span.GetTagItem("db.system.name").Should().Be("memory");
        span.GetTagItem("db.namespace").Should().Be("shop");
        span.GetTagItem("db.collection.name").Should().Be("orders");
        span.GetTagItem("qyl.instrumentation.domain").Should().Be("db.client");
        span.GetTagItem("server.port").Should().BeNull();
    }

    [Fact]
    public void Without_a_listener_a_starter_returns_null()
    {
        Spans.Source.HasListeners().Should().BeFalse();

        Spans.DbClient(DbSystemName.Postgresql, null, null, null, null).Should().BeNull();
        Spans.Work("checkout").Should().BeNull();
    }

    [Fact]
    public void Work_starts_an_internal_span_stamped_app()
    {
        using var listener = new ApplicationSourceListener();

        using var span = Spans.Work("checkout");

        span!.DisplayName.Should().Be("checkout");
        span.Kind.Should().Be(ActivityKind.Internal);
        span.GetTagItem("qyl.instrumentation.domain").Should().Be("app");
    }

    [Fact]
    public void The_consumer_starter_parents_to_the_producer_context()
    {
        using var listener = new ApplicationSourceListener();
        var producer = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: true);

        using var span = IncubatingSpans.MessagingProcessConsumer(MessagingSystem.Kafka, "process",
            batchMessageCount: null, destinationName: "orders", destinationTemplate: null, producer: producer);

        span!.Kind.Should().Be(ActivityKind.Consumer);
        span.DisplayName.Should().Be("process orders");
        span.TraceId.Should().Be(producer.TraceId);
        span.ParentSpanId.Should().Be(producer.SpanId);
        span.GetTagItem("messaging.system").Should().Be("kafka");
        span.GetTagItem("messaging.operation.type").Should().Be("process");
        span.GetTagItem("qyl.instrumentation.domain").Should().Be("messaging.process");
    }

    [Fact]
    public void The_agent_starter_fixes_its_operation()
    {
        using var listener = new ApplicationSourceListener();

        using var span = IncubatingSpans.GenAiInvokeAgent(agentDescription: null, agentName: "Planner",
            conversationId: "conv-1", dataSourceId: null, outputType: null, requestChoiceCount: null, requestSeed: null);

        span!.DisplayName.Should().Be("invoke_agent Planner");
        span.Kind.Should().Be(ActivityKind.Internal);
        span.GetTagItem("gen_ai.operation.name").Should().Be("invoke_agent");
        span.GetTagItem("gen_ai.agent.name").Should().Be("Planner");
        span.GetTagItem("qyl.instrumentation.domain").Should().Be("gen_ai.invoke_agent");
        typeof(IncubatingSpans).GetMethod(nameof(IncubatingSpans.GenAiInvokeAgent))!.GetParameters()
            .Select(static p => p.Name).Should().NotContain("operationName");
    }

    [Fact]
    public void Span_names_are_generated_for_the_families_qyl_instruments()
    {
        SpanNames.HttpServer("GET", "/orders/{id}").Should().Be("GET /orders/{id}");
        SpanNames.HttpServer("_OTHER", "/orders").Should().Be("HTTP /orders");
        SpanNames.HttpServer(null, null).Should().Be("HTTP");
        SpanNames.RpcGrpcCallClient("_OTHER", "grpc").Should().Be("grpc");
        SpanNames.RpcGrpcCallClient("Greeter/SayHello", "grpc").Should().Be("Greeter/SayHello");
        SpanNames.MessagingProcessConsumer("process", null, "orders", null, null).Should().Be("process orders");
        SpanNames.DbClient(null, null, null, null, null, null, null, "postgresql").Should().Be("postgresql");
    }
}
