using System.Diagnostics;
using System.Reflection;
using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.Activities;
using Qyl.Telemetry.SemanticConventions.Incubating.Views;
using Qyl.Telemetry.SemanticConventions.Views;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
/// Typed read-only views over <see cref="SpanRecord"/> (ADR D8), generated next to the starters:
/// they expose only what the collector persists, read the session spine through the trace and
/// never as an attribute, and give a database span one store identity.
/// </summary>
public sealed class ViewGenerationTests
{
    private static SpanRecord Record(
        string name,
        ActivityKind kind,
        Dictionary<string, object?> attributes,
        string service = "orders-api") =>
        new(
            traceId: "4bf92f3577b34da6a3ce929d0e0e4736",
            spanId: "00f067aa0ba902b7",
            parentSpanId: null,
            name: name,
            kind: kind,
            startTime: DateTimeOffset.UnixEpoch,
            endTime: DateTimeOffset.UnixEpoch.AddMilliseconds(5),
            status: ActivityStatusCode.Unset,
            statusDescription: null,
            scopeName: "Qyl.Telemetry.Application",
            scopeVersion: SchemaUrl.QylVersion,
            schemaUrl: SchemaUrl.Qyl,
            attributes: attributes,
            resource: new Dictionary<string, object?> { ["service.name"] = service },
            events: [],
            links: []);

    [Fact]
    public void The_db_client_view_exposes_service_system_namespace_store_and_app()
    {
        var properties = typeof(DbClientSpan).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(static p => p.Name)
            .ToList();

        properties.Should().Contain(["Service", "System", "Namespace", "Store", "App"]);
        properties.Should().NotContain("QueryText");
        typeof(DbClientSpan).GetProperty("System")!.PropertyType.Should().Be<DbSystemName>();
        typeof(DbClientSpan).GetProperty("Store")!.PropertyType.Should().Be<StoreIdentity>();
    }

    [Fact]
    public void A_view_reads_no_key_the_collector_drops()
    {
        DbClientSpan.Keys.Should().NotBeEmpty().And.OnlyContain(static key => RegistryPolicy.IsPersisted(key));
        DbClientSpan.Keys.Should().NotContain("db.query.text");

        GenAiInvokeAgentSpan.Keys.Should().Contain("gen_ai.agent.name")
            .And.NotContain("gen_ai.conversation.id")
            .And.NotContain("gen_ai.agent.description")
            .And.OnlyContain(static key => RegistryPolicy.IsPersisted(key));
        typeof(GenAiInvokeAgentSpan).GetProperty("ConversationId").Should().BeNull("the session spine is read through the trace");
    }

    [Fact]
    public void The_view_selects_its_spans_and_reads_them_typed()
    {
        var spans = new[]
        {
            Record("write orders", ActivityKind.Client, new Dictionary<string, object?>
            {
                ["db.system.name"] = "postgresql",
                ["db.namespace"] = "shop",
                ["db.collection.name"] = "orders",
                ["server.address"] = "db.internal",
                ["server.port"] = 5432L,
                ["db.query.text"] = "insert into orders values ($1)",
                ["qyl.app.order.id"] = "ord-1042",
                ["qyl.app.order.lines"] = 3L,
            }),
            Record("GET /orders", ActivityKind.Server, new Dictionary<string, object?> { ["http.request.method"] = "GET" }),
            Record("checkout", ActivityKind.Internal, new Dictionary<string, object?> { ["qyl.instrumentation.domain"] = "app" }),
        };

        var db = spans.DbClient().Should().ContainSingle().Subject;

        db.Service.Should().Be("orders-api");
        db.System.Should().Be(DbSystemName.Postgresql);
        db.Namespace.Should().Be("shop");
        db.CollectionName.Should().Be("orders");
        db.ServerPort.Should().Be(5432L);
        db.Store.Should().Be(new StoreIdentity("postgresql", "db.internal", 5432L, "shop"));
        db.App.Should().Equal(new Dictionary<string, string> { ["order.id"] = "ord-1042", ["order.lines"] = "3" });
        db.Record.Name.Should().Be("write orders");
    }

    [Fact]
    public void A_view_of_a_fixed_operation_matches_only_that_operation()
    {
        var spans = new[]
        {
            Record("invoke_agent Planner", ActivityKind.Internal, new Dictionary<string, object?>
            {
                ["gen_ai.operation.name"] = "invoke_agent",
                ["gen_ai.agent.name"] = "Planner",
            }),
            Record("execute_tool lookup_stock", ActivityKind.Internal, new Dictionary<string, object?>
            {
                ["gen_ai.operation.name"] = "execute_tool",
                ["gen_ai.tool.name"] = "lookup_stock",
            }),
        };

        spans.GenAiInvokeAgent().Should().ContainSingle().Which.AgentName.Should().Be("Planner");
        spans.GenAiExecuteTool().Should().ContainSingle().Which.ToolName.Should().Be("lookup_stock");
    }

    [Fact]
    public void SpanRecord_sits_beside_SpanDefinition()
    {
        typeof(SpanRecord).Namespace.Should().Be(typeof(SpanDefinition<>).Namespace);
        typeof(SpanRecord).Assembly.Should().BeSameAs(typeof(SpanDefinition<>).Assembly);
        typeof(StoreIdentity).Assembly.Should().BeSameAs(typeof(SpanDefinition<>).Assembly);
    }
}
