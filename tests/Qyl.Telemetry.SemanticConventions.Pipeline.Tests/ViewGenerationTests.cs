using System.Diagnostics;
using System.Reflection;
using Qyl.Telemetry.SemanticConventions.Activities;
using Qyl.Telemetry.SemanticConventions.Incubating.Views;
using Qyl.Telemetry.SemanticConventions.Views;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;
/// <summary>
/// Typed read-only views over <see cref = "SpanRecord"/> (ADR D8), generated next to the starters:
/// they expose only what the collector persists, read the session spine through the trace and
/// never as an attribute, and give a database span one store identity.
/// </summary>
[TestClass]
public sealed class ViewGenerationTests
{
    private static SpanRecord Record(string name, ActivityKind kind, Dictionary<string, object?> attributes, string service = "orders-api") => new("4bf92f3577b34da6a3ce929d0e0e4736", "00f067aa0ba902b7", name)
    {
        Kind = (SpanKind)((int)kind + 1),
        EndTimeUnixNano = 5_000_000,
        Scope = new InstrumentationScopeRecord
        {
            Name = "Qyl.Telemetry.Application",
            Version = SchemaUrl.QylVersion,
            SchemaUrl = SchemaUrl.Qyl,
        },
        Attributes = attributes,
        Resource = new ResourceRecord(new Dictionary<string, object?> { ["service.name"] = service }),
    };
    [TestMethod]
    public void The_db_client_view_exposes_service_system_namespace_store_and_app()
    {
        var properties = typeof(DbClientSpan).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(static p => p.Name).ToList();
        foreach (var property in new[]
        {
            "Service",
            "System",
            "Namespace",
            "Store",
            "App"
        })
            Assert.Contains(property, properties);
        Assert.DoesNotContain("QueryText", properties);
        Assert.AreEqual(typeof(DbSystemName), typeof(DbClientSpan).GetProperty("System")!.PropertyType);
        Assert.AreEqual(typeof(StoreIdentity), typeof(DbClientSpan).GetProperty("Store")!.PropertyType);
    }

    [TestMethod]
    public void A_view_reads_no_key_the_collector_drops()
    {
        Assert.IsNotEmpty(DbClientSpan.Keys);
        Assert.IsTrue(DbClientSpan.Keys.All(static key => RegistryPolicy.IsPersisted(key)));
        Assert.DoesNotContain("db.query.text", DbClientSpan.Keys);
        Assert.Contains("gen_ai.agent.name", GenAiInvokeAgentSpan.Keys);
        Assert.DoesNotContain("gen_ai.conversation.id", GenAiInvokeAgentSpan.Keys);
        Assert.DoesNotContain("gen_ai.agent.description", GenAiInvokeAgentSpan.Keys);
        Assert.IsTrue(GenAiInvokeAgentSpan.Keys.All(static key => RegistryPolicy.IsPersisted(key)));
        Assert.IsNull(typeof(GenAiInvokeAgentSpan).GetProperty("ConversationId"), "the session spine is read through the trace");
    }

    [TestMethod]
    public void The_view_selects_its_spans_and_reads_them_typed()
    {
        var spans = new[]
        {
            Record("write orders", ActivityKind.Client, new Dictionary<string, object?> { ["db.system.name"] = "postgresql", ["db.namespace"] = "shop", ["db.collection.name"] = "orders", ["server.address"] = "db.internal", ["server.port"] = 5432L, ["db.query.text"] = "insert into orders values ($1)", ["qyl.app.order.id"] = "ord-1042", ["qyl.app.order.lines"] = 3L, }),
            Record("GET /orders", ActivityKind.Server, new Dictionary<string, object?> { ["http.request.method"] = "GET" }),
            Record("checkout", ActivityKind.Internal, new Dictionary<string, object?> { ["qyl.instrumentation.domain"] = "app" }),
        };
        var db = Assert.ContainsSingle(spans.DbClient());
        Assert.AreEqual("orders-api", db.Service);
        Assert.AreEqual(DbSystemName.Postgresql, db.System);
        Assert.AreEqual("shop", db.Namespace);
        Assert.AreEqual("orders", db.CollectionName);
        Assert.AreEqual(5432L, db.ServerPort);
        Assert.AreEqual(new StoreIdentity("postgresql", "db.internal", 5432L, "shop"), db.Store);
        Assert.AreEquivalent(new Dictionary<string, string> { ["order.id"] = "ord-1042", ["order.lines"] = "3" }, db.App);
        Assert.AreEqual("write orders", db.Record.Name);
    }

    [TestMethod]
    public void A_view_of_a_fixed_operation_matches_only_that_operation()
    {
        var spans = new[]
        {
            Record("invoke_agent Planner", ActivityKind.Internal, new Dictionary<string, object?> { ["gen_ai.operation.name"] = "invoke_agent", ["gen_ai.agent.name"] = "Planner", }),
            Record("execute_tool lookup_stock", ActivityKind.Internal, new Dictionary<string, object?> { ["gen_ai.operation.name"] = "execute_tool", ["gen_ai.tool.name"] = "lookup_stock", }),
        };
        Assert.AreEqual("Planner", Assert.ContainsSingle(spans.GenAiInvokeAgent()).AgentName);
        Assert.AreEqual("lookup_stock", Assert.ContainsSingle(spans.GenAiExecuteTool()).ToolName);
    }

    [TestMethod]
    public void SpanRecord_sits_beside_SpanDefinition()
    {
        Assert.AreEqual(typeof(SpanDefinition<>).Namespace, typeof(SpanRecord).Namespace);
        Assert.AreSame(typeof(SpanDefinition<>).Assembly, typeof(SpanRecord).Assembly);
        Assert.AreSame(typeof(SpanDefinition<>).Assembly, typeof(StoreIdentity).Assembly);
    }

    [TestMethod]
    [DataRow(99UL, 100UL, 0L)]
    [DataRow(100UL, 299UL, 1L)]
    [DataRow(299UL, 100UL, -1L)]
    public void Duration_converts_the_difference_before_rounding_to_dotnet_ticks(ulong start, ulong end, long ticks)
    {
        var span = new SpanRecord("4bf92f3577b34da6a3ce929d0e0e4736", "00f067aa0ba902b7", "operation")
        {
            StartTimeUnixNano = start,
            EndTimeUnixNano = end,
        };
        Assert.AreEqual(TimeSpan.FromTicks(ticks), span.Duration);
    }
}
