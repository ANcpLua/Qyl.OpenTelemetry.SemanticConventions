using System.Reflection;
using System.Text.RegularExpressions;
using ANcpLua.Roslyn.Utilities.Testing.MSBuild;
using AwesomeAssertions;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
/// The collector's deny lists moved out of qyl's eng/config/collector-semantic-policy.json into the
/// registry (registry/qyl/policy.yaml); the generated <see cref="RegistryPolicy"/> is the one table
/// the views, QYL0113 and the collector catalog read. The expected lists are that policy file's
/// <c>deniedExactKeys</c>, <c>deniedKeyTokens</c> and <c>deniedTokenExemptKeys</c> on 2026-10-14,
/// with <c>gen_ai.agent.name</c> un-denied (ADR answer 3).
/// </summary>
public sealed class RegistryPolicyTests
{
    private static readonly string[] CollectorDeniedExactKeys =
    [
        "code.file.path", "code.stacktrace", "db.query.text", "exception.message", "exception.stacktrace",
        "executor.input", "executor.output", "gen_ai.agent.description", "gen_ai.agent.id",
        "gen_ai.conversation.id", "gen_ai.data_source.id", "gen_ai.evaluation.explanation",
        "gen_ai.evaluation.name", "gen_ai.input.messages", "gen_ai.output.messages", "gen_ai.prompt.name",
        "gen_ai.response.id", "gen_ai.retrieval.documents", "gen_ai.retrieval.query.text",
        "gen_ai.system_instructions", "gen_ai.tool.call.arguments", "http.request.header",
        "http.response.header", "gen_ai.tool.call.id", "gen_ai.tool.call.result", "gen_ai.tool.definitions",
        "gen_ai.tool.description", "gen_ai.workflow.name", "host.id", "host.ip", "host.mac", "host.name",
        "mcp.resource.uri", "mcp.session.id", "message.content", "rpc.request.metadata",
        "rpc.response.metadata", "service.instance.id", "session.id", "session.previous_id", "url.full",
        "url.query",
    ];

    private static readonly string[] CollectorDeniedKeyTokens =
    [
        "access_token", "api-key", "api_key", "apikey", "authorization", "body", "completion", "cookie",
        "credential", "definition", "document", "fingerprint", "id_token", "instruction", "jwt", "message",
        "password", "private_key", "prompt", "query", "refresh_token", "result", "secret", "set-cookie", "token",
    ];

    private static readonly string[] CollectorTokenExemptKeys =
    [
        "db.query.summary", "gen_ai.token.type", "messaging.kafka.message.offset", "messaging.message.body.size",
        "messaging.message.id",
    ];

    [Fact]
    public void Every_collector_denied_exact_key_except_the_agent_name_is_not_persisted()
    {
        RegistryPolicy.NotPersistedKeys.Should().BeEquivalentTo(CollectorDeniedExactKeys);
        RegistryPolicy.NotPersistedKeys.Should().BeInAscendingOrder(StringComparer.Ordinal);
        RegistryPolicy.NotPersistedPrefixes.Should().Equal("enduser.", "user.");
    }

    [Fact]
    public void The_token_list_matches_the_collectors()
    {
        RegistryPolicy.DeniedKeyTokens.Should().Equal(CollectorDeniedKeyTokens);
        RegistryPolicy.TokenExemptKeys.Should().Contain(CollectorTokenExemptKeys);
        RegistryPolicy.VendorKeys.Should().Contain("messaging.masstransit.message_id").And.Contain("nservicebus.message_intent");
    }

    [Fact]
    public void The_generated_table_equals_the_registry_annotations()
    {
        var policy = File.ReadAllText(RepoPath("registry", "qyl", "policy.yaml"));

        Annotated(policy, "persisted: false").Should().BeEquivalentTo(RegistryPolicy.NotPersistedKeys);
        Annotated(policy, "token_exempt: true").Should().BeEquivalentTo(RegistryPolicy.TokenExemptKeys);
    }

    [Theory]
    [InlineData("gen_ai.agent.name", true)]
    [InlineData("db.query.summary", true)]
    [InlineData("messaging.message.id", true)]
    [InlineData("gen_ai.usage.input_tokens", true)]
    [InlineData("messaging.masstransit.message_id", true)]
    [InlineData("qyl.app.order.id", true)]
    [InlineData("exception.message", false)]
    [InlineData("http.request.header.authorization", false)]
    [InlineData("user.email", false)]
    [InlineData("enduser.id", false)]
    [InlineData("qyl.app.order.message", false)]
    [InlineData("gen_ai.request.max_tokens", true)]
    [InlineData("gen_ai.usage.text.input_tokens", true)]
    [InlineData("qyl.app.user.email", false)]
    [InlineData("qyl.app.url.full", false)]
    [InlineData("qyl.app.enduser.id", false)]
    [InlineData("db.odp.user.statement", false)]
    [InlineData("nservicebus.processing_machine", false)]
    [InlineData("qyl.app.API_KEY", false)]
    public void IsPersisted_applies_the_collectors_rule(string key, bool persisted)
    {
        RegistryPolicy.IsPersisted(key).Should().Be(persisted);
    }

    [Fact]
    public void Every_generated_attribute_reference_agrees_with_the_table()
    {
        var references = new[] { typeof(SpanDefinition<>).Assembly, typeof(Incubating.Spans.GenAiIncubatingSpanDefinitions).Assembly }
            .SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type.Namespace?.EndsWith(".Spans", StringComparison.Ordinal) == true)
            .SelectMany(static type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Select(static field => field.GetValue(null))
            .SelectMany(static definition => (IReadOnlyList<AttributeRef>)definition!.GetType().GetProperty("Attributes")!.GetValue(definition)!)
            .ToList();

        references.Should().NotBeEmpty();
        references.Should().OnlyContain(static reference => reference.IsPersisted == RegistryPolicy.IsPersisted(reference.Name));
        references.Should().Contain(static reference => !reference.IsPersisted);
    }

    private static IEnumerable<string> Annotated(string policy, string annotation) =>
        Regex.Matches(policy, @"- ref: (?<key>\S+)\s+annotations: \{qyl: \{" + Regex.Escape(annotation) + @"\}\}")
            .Select(static match => match.Groups["key"].Value);

    private static string RepoPath(params string[] parts)
    {
        string root = RepositoryRoot.LocateFrom(AppContext.BaseDirectory, "Qyl.Telemetry.SemanticConventions.slnx");
        return Path.Combine([root, .. parts]);
    }
}
