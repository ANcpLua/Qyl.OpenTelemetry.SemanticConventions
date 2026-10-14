using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.Spans;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
/// A requirement level with a condition is a mapping whose key names the level. lib.j2 used to map
/// every mapping to ConditionallyRequired, so <c>{recommended: text}</c> attributes such as
/// <c>network.peer.address</c> on <c>db.client</c> turned into conditional ones.
/// </summary>
public sealed class RequirementLevelMappingTests
{
    private static AttributeRef DbClient(string key) =>
        DbSpanDefinitions.DbClient.Attributes.Single(attribute => attribute.Name == key);

    [Fact]
    public void Recommended_with_a_condition_maps_to_recommended()
    {
        var peer = DbClient("network.peer.address");

        peer.RequirementLevel.Should().Be(RequirementLevel.Recommended);
        peer.Condition.Should().Be("If applicable for this database system.");
    }

    [Fact]
    public void Conditionally_required_keeps_its_level_and_condition()
    {
        var @namespace = DbClient("db.namespace");

        @namespace.RequirementLevel.Should().Be(RequirementLevel.ConditionallyRequired);
        @namespace.Condition.Should().Be("If available.");
    }

    [Fact]
    public void A_level_without_a_condition_carries_none()
    {
        DbClient("server.address").Should().BeEquivalentTo(new
        {
            RequirementLevel = RequirementLevel.Recommended,
            Condition = (string?)null,
        });
        DbClient("db.system.name").RequirementLevel.Should().Be(RequirementLevel.Required);
        DbClient("db.query.parameter").RequirementLevel.Should().Be(RequirementLevel.OptIn);
    }
}
