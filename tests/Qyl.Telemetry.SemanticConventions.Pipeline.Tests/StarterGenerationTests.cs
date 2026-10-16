using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using ANcpLua.Roslyn.Utilities.Testing;
using Qyl.Telemetry.SemanticConventions.Activities;
using Qyl.Telemetry.SemanticConventions.Incubating.Activities;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;
// Inside Qyl.Telemetry.SemanticConventions.* the simple name Spans is the definitions namespace.
using Spans = global::Qyl.Spans;

/// <summary>
/// Generated span starters (ADR D2): parameters follow the requirement levels mechanically, the
/// kind comes from the group, the name from the <c>qyl.span_name</c> chain, the source is the
/// registry-owned application scope, and the group's <c>qyl.instrumentation.domain</c> is stamped.
/// </summary>
[ResourceLock(ApplicationSourceListeners.Name)]
[TestClass]
public sealed class StarterGenerationTests
{
    private static ParameterInfo[] DbClientParameters() => typeof(Spans).GetMethod(nameof(Spans.DbClient))!.GetParameters();
    [TestMethod]
    public void The_required_attribute_is_positional_and_typed()
    {
        var system = DbClientParameters()[0];
        Assert.AreEqual("system", system.Name);
        Assert.AreEqual(typeof(DbSystemName), system.ParameterType);
        Assert.IsFalse(system.IsOptional);
    }

    [TestMethod]
    public void Conditionally_required_parameters_are_nullable_and_have_no_default()
    {
        var parameters = DbClientParameters().ToDictionary(static p => p.Name!);
        foreach (var name in new[]
        {
            "namespace",
            "collectionName",
            "operationName",
            "serverPort"
        })
        {
            Assert.IsTrue(parameters.ContainsKey(name));
            Assert.IsFalse(parameters[name].IsOptional, $"{name} is conditionally required");
            var type = parameters[name].ParameterType;
            Assert.IsTrue((type == typeof(string) || Nullable.GetUnderlyingType(type)is not null), $"{name} is nullable");
        }
    }

    [TestMethod]
    public void Recommended_parameters_default_to_null()
    {
        var parameters = DbClientParameters().ToDictionary(static p => p.Name!);
        foreach (var name in new[]
        {
            "querySummary",
            "queryText",
            "storedProcedureName",
            "serverAddress",
            "operationBatchSize",
            "networkPeerAddress",
            "networkPeerPort"
        })
        {
            Assert.IsTrue(parameters.ContainsKey(name));
            Assert.IsTrue(parameters[name].IsOptional, $"{name} is recommended");
            Assert.IsNull(parameters[name].DefaultValue);
        }
    }

    [TestMethod]
    public void OptIn_and_end_phase_attributes_get_no_parameter()
    {
        Assert.DoesNotContain("queryParameter", DbClientParameters().Select(static p => p.Name));
        Assert.DoesNotContain("responseReturnedRows", DbClientParameters().Select(static p => p.Name));
        Assert.DoesNotContain("responseStatusCode", DbClientParameters().Select(static p => p.Name));
        Assert.DoesNotContain("errorType", DbClientParameters().Select(static p => p.Name));
    }

    [TestMethod]
    public void Omitting_a_conditional_parameter_is_a_compile_error()
    {
        var result = Compile.Source("""
                using Qyl;
                using Qyl.Telemetry.SemanticConventions.Activities;

                public static class Store
                {
                    public static void Write() =>
                        Spans.DbClient(DbSystemName.Postgresql, collectionName: "orders", operationName: "write", serverPort: null)?.Dispose();
                }
                """).WithReference(typeof(Spans).Assembly).WithReference<Activity>().Build();
        Assert.IsTrue(result.HasError("CS7036"));
        Assert.IsTrue(result.Diagnostics.Any(static d => d.Id == "CS7036" && d.GetMessage(CultureInfo.InvariantCulture).Contains("namespace", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("SELECT orders", "SELECT", "orders", "shop", null, null, "SELECT orders")]
    [DataRow(null, "write", "orders", "shop", null, null, "write orders")]
    [DataRow(null, "write", null, "shop", null, null, "write shop")]
    [DataRow(null, null, "orders", "shop", null, null, "orders")]
    [DataRow(null, null, null, null, "db.internal", 5432L, "db.internal:5432")]
    [DataRow(null, null, null, null, null, null, "postgresql")]
    public void The_name_follows_the_span_name_chain(string? summary, string? operation, string? collection, string? @namespace, string? address, long? port, string expected)
    {
        using var listener = new ApplicationSourceListener();
        using var span = Spans.DbClient(DbSystemName.Postgresql, collectionName: collection, @namespace: @namespace, operationName: operation, serverPort: port, querySummary: summary, serverAddress: address);
        Assert.IsNotNull(span);
        Assert.AreNotEqual(default(ActivityTraceId), span.TraceId);
        Assert.AreNotEqual(default(ActivitySpanId), span.SpanId);
        Assert.AreEqual(ActivityStatusCode.Unset, span.Status);
        Assert.AreEqual("Qyl.Telemetry.Application", span.Source.Name);
        Assert.AreEqual(SchemaUrl.QylVersion, span.Source.Version);
        Assert.AreEqual(SchemaUrl.Qyl, span.Source.TelemetrySchemaUrl);
        Assert.AreEqual(expected, span.DisplayName);
        Assert.AreEqual(ActivityKind.Client, span.Kind);
        Assert.AreEqual("db.client", span.GetTagItem("qyl.instrumentation.domain"));
    }

    [TestMethod]
    public void The_starter_starts_a_client_span_on_the_application_scope_with_its_domain()
    {
        using var listener = new ApplicationSourceListener();
        using var span = Spans.DbClient(DbSystemName.Custom("memory"), @namespace: "shop", collectionName: "orders", operationName: "write", serverPort: null);
        Assert.IsNotNull(span);
        Assert.AreNotEqual(default(ActivityTraceId), span.TraceId);
        Assert.AreNotEqual(default(ActivitySpanId), span.SpanId);
        Assert.AreEqual(ActivityStatusCode.Unset, span.Status);
        Assert.AreEqual("Qyl.Telemetry.Application", span.Source.Name);
        Assert.AreEqual(SchemaUrl.QylVersion, span.Source.Version);
        Assert.AreEqual(SchemaUrl.Qyl, span.Source.TelemetrySchemaUrl);
        Assert.AreEqual(ActivityKind.Client, span.Kind);
        Assert.AreEqual("write orders", span.DisplayName);
        Assert.AreEqual("memory", span.GetTagItem("db.system.name"));
        Assert.AreEqual("shop", span.GetTagItem("db.namespace"));
        Assert.AreEqual("orders", span.GetTagItem("db.collection.name"));
        Assert.AreEqual("db.client", span.GetTagItem("qyl.instrumentation.domain"));
        Assert.IsNull(span.GetTagItem("server.port"));
    }

    [TestMethod]
    public void Without_a_listener_a_starter_returns_null()
    {
        Assert.IsFalse(Spans.Source.HasListeners());
        Assert.IsNull(Spans.DbClient(DbSystemName.Postgresql, null, null, null, null));
        Assert.IsNull(Spans.Work("checkout"));
    }

    [TestMethod]
    public void Work_starts_an_internal_span_stamped_app()
    {
        using var listener = new ApplicationSourceListener();
        using var span = Spans.Work("checkout");
        Assert.IsNotNull(span);
        Assert.AreNotEqual(default(ActivityTraceId), span.TraceId);
        Assert.AreNotEqual(default(ActivitySpanId), span.SpanId);
        Assert.AreEqual(ActivityStatusCode.Unset, span.Status);
        Assert.AreEqual("Qyl.Telemetry.Application", span.Source.Name);
        Assert.AreEqual(SchemaUrl.QylVersion, span.Source.Version);
        Assert.AreEqual(SchemaUrl.Qyl, span.Source.TelemetrySchemaUrl);
        Assert.AreEqual("checkout", span.DisplayName);
        Assert.AreEqual(ActivityKind.Internal, span.Kind);
        Assert.AreEqual("app", span.GetTagItem("qyl.instrumentation.domain"));
    }

    [TestMethod]
    public void The_consumer_starter_parents_to_the_producer_context()
    {
        using var listener = new ApplicationSourceListener();
        var producer = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: true);
        using var span = IncubatingSpans.MessagingProcessConsumer(MessagingSystem.Kafka, "process", batchMessageCount: null, destinationName: "orders", destinationTemplate: null, producer: producer);
        Assert.IsNotNull(span);
        Assert.AreNotEqual(default(ActivityTraceId), span.TraceId);
        Assert.AreNotEqual(default(ActivitySpanId), span.SpanId);
        Assert.AreEqual(ActivityStatusCode.Unset, span.Status);
        Assert.AreEqual("Qyl.Telemetry.Application", span.Source.Name);
        Assert.AreEqual(SchemaUrl.QylVersion, span.Source.Version);
        Assert.AreEqual(SchemaUrl.Qyl, span.Source.TelemetrySchemaUrl);
        Assert.AreEqual(ActivityKind.Consumer, span.Kind);
        Assert.AreEqual("process orders", span.DisplayName);
        Assert.AreEqual(producer.TraceId, span.TraceId);
        Assert.AreEqual(producer.SpanId, span.ParentSpanId);
        Assert.AreEqual("kafka", span.GetTagItem("messaging.system"));
        Assert.AreEqual("process", span.GetTagItem("messaging.operation.type"));
        Assert.AreEqual("messaging.process", span.GetTagItem("qyl.instrumentation.domain"));
    }

    [TestMethod]
    public void The_agent_starter_fixes_its_operation()
    {
        using var listener = new ApplicationSourceListener();
        using var span = IncubatingSpans.GenAiInvokeAgent(agentDescription: null, agentName: "Planner", conversationId: "conv-1", dataSourceId: null, outputType: null, requestChoiceCount: null, requestSeed: null);
        Assert.IsNotNull(span);
        Assert.AreNotEqual(default(ActivityTraceId), span.TraceId);
        Assert.AreNotEqual(default(ActivitySpanId), span.SpanId);
        Assert.AreEqual(ActivityStatusCode.Unset, span.Status);
        Assert.AreEqual("Qyl.Telemetry.Application", span.Source.Name);
        Assert.AreEqual(SchemaUrl.QylVersion, span.Source.Version);
        Assert.AreEqual(SchemaUrl.Qyl, span.Source.TelemetrySchemaUrl);
        Assert.AreEqual("invoke_agent Planner", span.DisplayName);
        Assert.AreEqual(ActivityKind.Internal, span.Kind);
        Assert.AreEqual("invoke_agent", span.GetTagItem("gen_ai.operation.name"));
        Assert.AreEqual("Planner", span.GetTagItem("gen_ai.agent.name"));
        Assert.AreEqual("gen_ai.invoke_agent", span.GetTagItem("qyl.instrumentation.domain"));
        Assert.DoesNotContain("operationName", typeof(IncubatingSpans).GetMethod(nameof(IncubatingSpans.GenAiInvokeAgent))!.GetParameters().Select(static p => p.Name));
    }

    [TestMethod]
    public void Span_names_are_generated_for_the_families_qyl_instruments()
    {
        Assert.AreEqual("GET /orders/{id}", SpanNames.HttpServer("GET", "/orders/{id}"));
        Assert.AreEqual("HTTP /orders", SpanNames.HttpServer("_OTHER", "/orders"));
        Assert.AreEqual("HTTP", SpanNames.HttpServer(null, null));
        Assert.AreEqual("grpc", SpanNames.RpcGrpcCallClient("_OTHER", "grpc"));
        Assert.AreEqual("Greeter/SayHello", SpanNames.RpcGrpcCallClient("Greeter/SayHello", "grpc"));
        Assert.AreEqual("process orders", SpanNames.MessagingProcessConsumer("process", null, "orders", null, null));
        Assert.AreEqual("postgresql", SpanNames.DbClient(null, null, null, null, null, null, null, "postgresql"));
    }
}
