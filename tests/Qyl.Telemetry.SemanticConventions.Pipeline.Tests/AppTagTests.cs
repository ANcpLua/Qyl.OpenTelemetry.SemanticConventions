using System.Diagnostics;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;
/// <summary>
/// Business keys go through <c>SetAppTag</c>, which writes the registry's <c>qyl.app</c> template
/// key; the caller never types the prefix (ADR D3, answer 2).
/// </summary>
[TestClass]
public sealed class AppTagTests
{
    [TestMethod]
    public void SetAppTag_writes_under_the_qyl_app_prefix()
    {
        using var activity = new Activity("app-tag");
        Assert.AreSame(activity, activity.SetAppTag("order.id", "ord-1042"));
        Assert.AreEqual("ord-1042", activity.GetTagItem("qyl.app.order.id"));
        Assert.IsNull(activity.GetTagItem("order.id"));
    }

    [TestMethod]
    public void SetAppTag_keeps_long_double_and_bool_values_typed()
    {
        using var activity = new Activity("app-tag");
        activity.SetAppTag("order.lines", 3L).SetAppTag("order.total", 41.5).SetAppTag("order.express", true);
        Assert.AreEqual(3L, Assert.IsInstanceOfType<long>(activity.GetTagItem("qyl.app.order.lines")));
        Assert.AreEqual(41.5, Assert.IsInstanceOfType<double>(activity.GetTagItem("qyl.app.order.total")));
        Assert.IsTrue(Assert.IsInstanceOfType<bool>(activity.GetTagItem("qyl.app.order.express")));
    }
}
