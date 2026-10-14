using System.Reflection;
using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.Incubating.Spans;
using Qyl.Telemetry.SemanticConventions.Spans;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
/// Span rows are keyed by refinement id and grouped by type. Before, the spans filter deduplicated
/// by type, so the three gen_ai skill spans, which share <c>gen_ai.execute_tool.internal</c> with
/// the generic execute_tool span, collapsed into one row and lost their span-name rules and
/// <c>gen_ai.skill.*</c> requirement levels.
/// </summary>
public sealed class SpanGenerationTests
{
    private const string ExecuteTool = "gen_ai.execute_tool.internal";

    [Fact]
    public void The_three_skill_spans_are_rows_of_their_own_that_specialize_execute_tool()
    {
        var skills = new[]
        {
            GenAiIncubatingSpanDefinitions.GenAiExecuteToolLoadSkillInternal,
            GenAiIncubatingSpanDefinitions.GenAiExecuteToolReadSkillResourceInternal,
            GenAiIncubatingSpanDefinitions.GenAiExecuteToolCommandInternal,
        };

        skills.Select(static s => s.Id).Should().Equal(
            "gen_ai.execute_tool.load_skill.internal",
            "gen_ai.execute_tool.read_skill_resource.internal",
            "gen_ai.execute_tool.command.internal");
        skills.Should().OnlyContain(static s => s.Type == ExecuteTool && s.IsSpecialization && s.SpanKind == "internal");
        skills.Should().OnlyContain(static s => s.Attributes.Any(a => a.Name == "gen_ai.skill.name"
                                                                      && a.RequirementLevel == RequirementLevel.ConditionallyRequired));

        var generic = GenAiIncubatingSpanDefinitions.GenAiExecuteToolInternal;
        generic.Id.Should().Be(ExecuteTool);
        generic.IsSpecialization.Should().BeFalse();
        generic.Attributes.Should().NotContain(static a => a.Name.StartsWith("gen_ai.skill.", StringComparison.Ordinal));
    }

    [Fact]
    public void Each_skill_span_carries_its_own_span_name_rule()
    {
        GenAiIncubatingSpanDefinitions.GenAiExecuteToolInternal.SpanName
            .Should().Equal("execute_tool {gen_ai.tool.name}");
        GenAiIncubatingSpanDefinitions.GenAiExecuteToolLoadSkillInternal.SpanName.Should().Equal(
            "execute_tool {gen_ai.tool.name} {gen_ai.skill.name}",
            "execute_tool {gen_ai.tool.name}");
        GenAiIncubatingSpanDefinitions.GenAiExecuteToolReadSkillResourceInternal.SpanName.Should().Equal(
            "execute_tool {gen_ai.tool.name} {gen_ai.skill.name} {gen_ai.skill.resource.name}",
            "execute_tool {gen_ai.tool.name} {gen_ai.skill.resource.name}",
            "execute_tool {gen_ai.tool.name}");
        GenAiIncubatingSpanDefinitions.GenAiExecuteToolCommandInternal.SpanName.Should().Equal(
            "execute_tool {gen_ai.tool.name} {gen_ai.skill.name} {gen_ai.skill.resource.name}",
            "execute_tool {gen_ai.tool.name} {gen_ai.skill.resource.name}",
            "execute_tool {gen_ai.tool.name} {process.executable.name}",
            "execute_tool {gen_ai.tool.name}");
    }

    [Fact]
    public void A_specialization_without_a_rule_of_its_own_uses_its_types()
    {
        DbSpanDefinitions.DbPostgresqlClient.Type.Should().Be("db.sql.client");
        DbSpanDefinitions.DbPostgresqlClient.SpanName.Should().Equal(DbSpanDefinitions.DbSqlClient.SpanName);
        DbSpanDefinitions.DbClient.SpanName.Should().StartWith("{db.query.summary}").And.EndWith("{db.system.name}");
        MessagingIncubatingSpanDefinitions.MessagingKafkaProcessConsumer.SpanName
            .Should().Equal(MessagingIncubatingSpanDefinitions.MessagingProcessConsumer.SpanName);
    }

    [Fact]
    public void The_genai_provider_refinements_stay_excluded()
    {
        var fields = typeof(GenAiIncubatingSpanDefinitions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(static f => f.Name)
            .ToList();

        fields.Should().NotContain(static name => name.StartsWith("Openai", StringComparison.Ordinal)
                                                  || name.StartsWith("Anthropic", StringComparison.Ordinal)
                                                  || name.StartsWith("AzureAi", StringComparison.Ordinal)
                                                  || name.StartsWith("AwsBedrock", StringComparison.Ordinal));
        fields.Should().Contain("GenAiExecuteToolInternal").And.Contain("GenAiInvokeAgentInternal");
    }
}
