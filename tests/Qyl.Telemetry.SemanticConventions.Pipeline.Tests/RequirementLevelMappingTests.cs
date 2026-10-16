using Qyl.Telemetry.SemanticConventions.Spans;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;
/// <summary>
/// A requirement level with a condition is a mapping whose key names the level. lib.j2 used to map
/// every mapping to ConditionallyRequired, so <c>{recommended: text}</c> attributes such as
/// <c>network.peer.address</c> on <c>db.client</c> turned into conditional ones.
/// </summary>
[TestClass]
public sealed class RequirementLevelMappingTests
{
    private static AttributeRef DbClient(string key) => DbSpanDefinitions.DbClient.Attributes.Single(attribute => attribute.Name == key);
    [TestMethod]
    public void Recommended_with_a_condition_maps_to_recommended()
    {
        var peer = DbClient("network.peer.address");
        Assert.AreEqual(RequirementLevel.Recommended, peer.RequirementLevel);
        Assert.AreEqual("If applicable for this database system.", peer.Condition);
    }

    [TestMethod]
    public void Conditionally_required_keeps_its_level_and_condition()
    {
        var @namespace = DbClient("db.namespace");
        Assert.AreEqual(RequirementLevel.ConditionallyRequired, @namespace.RequirementLevel);
        Assert.AreEqual("If available.", @namespace.Condition);
    }

    [TestMethod]
    public void A_level_without_a_condition_carries_none()
    {
        var server = DbClient("server.address");
        Assert.AreEqual(RequirementLevel.Recommended, server.RequirementLevel);
        Assert.IsNull(server.Condition);
        Assert.AreEqual(RequirementLevel.Required, DbClient("db.system.name").RequirementLevel);
        Assert.AreEqual(RequirementLevel.OptIn, DbClient("db.query.parameter").RequirementLevel);
    }
}
